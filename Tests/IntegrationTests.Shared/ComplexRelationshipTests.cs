////////////////////////////////////////////////////////////////////////////
//
// Copyright 2016 Realm Inc.
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
// http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.
//
////////////////////////////////////////////////////////////////////////////
 
using NUnit.Framework;
using Realms;
using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;

namespace IntegrationTests.Shared
{
    [TestFixture, Preserve(AllMembers = true)]
    public class ComplexRelationshipTests
    {
        // based on user report, needs threee classes
        // will be searching on values coming from another table

        // Also show how a private int can be mapped to a different public struct property 
        public struct ServicedBy
        {
            private int _theInt;

            public ServicedBy(int anInt)
            {

                _theInt = anInt;
            }

            static public implicit operator ServicedBy(int intValue)
            {
                return new ServicedBy(intValue) { _theInt = intValue };
            }

            static public explicit operator int(ServicedBy sb)
            {
                return sb._theInt;
            }
        }


        public class MaintenanceLogEntry : RealmObject 
        {
            public DateTimeOffset DateOfService { get; set; }
            public int HoursAtService { get; set; }
            public string Notes { get; set; }
            public IList<ItemLogEntry> LoggedItems { get; }
            public string EngineSerial { get; set; }
            private int _servicedBy { get; set; }
            public ServicedBy ServicedBy {
                get { return (ServicedBy)_servicedBy; }
                set { _servicedBy = (int)value; }
            }
            public string DealerName { get; set; }
        }


        public class ItemLogEntry : RealmObject
        {
            public MaintenanceItem Item { get; set; }
            public bool Inspected { get; set; }
            public bool Replaced { get; set; }
        }


        public class MaintenanceItem : RealmObject
        {
            public string ShortName { get; set; }
            public string LongName { get; set; }
            public int InspectInterval { get; set; }
            public int ReplaceInterval { get; set; }
        }

        protected Realm _realm;

        [SetUp]
        public void Setup()
        {
            var conf = new RealmConfiguration("ComplexRel.realm");
            Realm.DeleteRealm(conf);
            _realm = Realm.GetInstance(conf);
        }

        [TearDown]
        public void TearDown()
        {
            _realm.Close();
            Realm.DeleteRealm(_realm.Config);
        }


        private void SetupRelatedSearchData()
        {

            _realm.Write(() => {
                var mi1 = _realm.CreateObject<MaintenanceItem>();
                mi1.ShortName = "Hanster Wheel";
                var mi2 = _realm.CreateObject<MaintenanceItem>();
                mi2.ShortName = "Hamster";
                var mi3 = _realm.CreateObject<MaintenanceItem>();
                mi3.ShortName = "Wheelnut";
                var mi4 = _realm.CreateObject<MaintenanceItem>();
                mi4.ShortName = "Hanster Feed Chute";

                var mi1_TF = _realm.CreateObject<ItemLogEntry>();
                mi1_TF.Inspected = true;
                mi1_TF.Replaced = false;
                mi1_TF.Item = mi1;

                var mi2_TT = _realm.CreateObject<ItemLogEntry>();
                mi2_TT.Inspected = true;
                mi2_TT.Replaced = true;
                mi2_TT.Item = mi2;

                var mi4_TT = _realm.CreateObject<ItemLogEntry>();
                mi4_TT.Inspected = true;
                mi4_TT.Replaced = true;
                mi4_TT.Item = mi4;

                var mle1 = _realm.CreateObject<MaintenanceLogEntry>();
                mle1.EngineSerial = "Thomas001";
                mle1.DateOfService = new DateTimeOffset(2015, 12, 1, 10, 30, 0, TimeSpan.Zero);
                mle1.LoggedItems.Add(mi1_TF);
                mle1.LoggedItems.Add(mi2_TT);

                var mle2 = _realm.CreateObject<MaintenanceLogEntry>();
                mle2.EngineSerial = "Thomas001";
                mle2.DateOfService = new DateTimeOffset(2016, 12, 1, 10, 30, 0, TimeSpan.Zero);
                mle2.LoggedItems.Add(mi1_TF);
                mle2.LoggedItems.Add(mi4_TT);
            });
        }


        [Test]
        public void AnyInRelatedSearch()
        {
            SetupRelatedSearchData();
            GetAllMaintenanceIntervals(99, "Thomas001");  // factored this way to keep similar to user bug report code
        }

        // matches user sample
        public void GetAllMaintenanceIntervals(int engineHours, string engineSerial)
        {
            var logsForThisEngine = _realm.All<MaintenanceLogEntry>().Where(log => log.EngineSerial == engineSerial);
            var llogs = logsForThisEngine.ToList();
            int foundInspected = 0;
            int foundReplaced = 0;
            foreach (var item in _realm.All<MaintenanceItem>()) {
                //Any not supported? hence ToList //TODO Realm workaround
                // the llogs is at this point a generic collection NOT using Realm queries
                // however lg.loggedItems is a RealmResults which is IEnumerable and to which we are applying standard Linq collection generics
                var itemInspectLogs = llogs.Where(
                    log => log.LoggedItems.Any(itemlog => itemlog.Item.ShortName == item.ShortName && itemlog.Inspected));
                foundInspected += itemInspectLogs.Count();
                var itemReplaceLogs = llogs.Where(
                    log => log.LoggedItems.Any(itemlog => itemlog.Item.ShortName == item.ShortName && itemlog.Replaced));
                foundReplaced += itemReplaceLogs.Count();

            }
            Assert.That(foundInspected, Is.EqualTo(4));
            Assert.That(foundReplaced, Is.EqualTo(2));
        }


        [Test, Explicit("Nested Any not supported see issue 651")]
        public void AnyInRelatedSearchNoList()
        {
            SetupRelatedSearchData();
            GetAllMaintenanceIntervalsNoList(99, "Thomas001");  // factored this way to keep similar to user bug report code
        }

        // matches user sample without ToList, proving Any not supported in this context
        public void GetAllMaintenanceIntervalsNoList(int engineHours, string engineSerial)
        {
            var serial = engineSerial; //TODO Realm workaround
            var logsForThisEngine = _realm.All<MaintenanceLogEntry>().Where(log => log.EngineSerial == serial);
            int foundInspected = 0;
            int foundReplaced = 0;
            foreach (var item in _realm.All<MaintenanceItem>()) {
                //Any not supported? hence ToList //TODO Realm workaround
                // the llogs is at this point a generic collection NOT using Realm queries
                // however lg.loggedItems is a RealmResults which is IEnumerable and to which we are applying standard Linq collection generics
                var itemInspectLogs = logsForThisEngine.Where(
                    log => log.LoggedItems.Any(itemlog => itemlog.Item.ShortName == item.ShortName && itemlog.Inspected));
                foundInspected += itemInspectLogs.Count();
                var itemReplaceLogs = logsForThisEngine.Where(
                    log => log.LoggedItems.Any(itemlog => itemlog.Item.ShortName == item.ShortName && itemlog.Replaced));
                foundReplaced += itemReplaceLogs.Count();

            }
            Assert.That(foundInspected, Is.EqualTo(4));
            Assert.That(foundReplaced, Is.EqualTo(2));
        }


        [Test]
        public void AnyInRelatedSearchRewritten()
        {
            SetupRelatedSearchData();
            GetAllMaintenanceIntervalsRewritten(99, "Thomas001");  // factored this way to keep similar to user bug report code
        }


        public void GetAllMaintenanceIntervalsRewritten(int engineHours, string engineSerial)
        {
            var logsForThisEngine = _realm.All<MaintenanceLogEntry>().Where(log => log.EngineSerial == engineSerial);
            int foundInspected = 0;
            int foundReplaced = 0;
            foreach (var mle in logsForThisEngine) {
                // the following Where is using standard Linq for Objects on the IEnumerable RealmList LoggedItems
                var itemInspectLogs = mle.LoggedItems.Where(itemlog => itemlog.Inspected);
                foundInspected += itemInspectLogs.Count();
                var itemReplaceLogs = mle.LoggedItems.Where(itemlog => itemlog.Replaced);
                foundReplaced += itemReplaceLogs.Count();
            }
            Assert.That(foundInspected, Is.EqualTo(4));
            Assert.That(foundReplaced, Is.EqualTo(2));
        }


    } // ComplexRelationshipTests

}