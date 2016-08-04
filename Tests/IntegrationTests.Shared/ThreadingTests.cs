﻿using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using Realms;

namespace IntegrationTests.Shared
{
    [TestFixture]
    public class ThreadingTests
    {
        [Test]
        public void RealmObjectsShouldKeepRealmAlive()
        {
            // Arrange
            var realm = new WeakReference(Realm.GetInstance());
            Person person = null;
            ((Realm)realm.Target).Write(() => { person = ((Realm)realm.Target).CreateObject<Person>(); });

            // Act
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Assert
            Assert.That(realm.IsAlive);
            Assert.That(((Realm)realm.Target).IsClosed, Is.False);
            Assert.That(person.IsValid);
        }

        [Test]
        public void FinalizedRealmsShouldNotInvalidateSiblingRealms()
        {
            // Arrange
            var realm = Realm.GetInstance();
            var realmThatWillBeFinalized = new WeakReference(Realm.GetInstance());
            Person person = null;
            realm.Write(() => { person = realm.CreateObject<Person>(); });


            // Act
            GC.Collect();
            GC.WaitForPendingFinalizers();

            // Assert
            Assert.That(realmThatWillBeFinalized.IsAlive, Is.False);
            Assert.That(person.IsValid);
        }
    }
}
