﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Cheese;
using System.Diagnostics;

namespace Mice.Tests
{
    [TestFixture]
    public class AllTests
    {
        [SetUp]
        public void Setup()
        {
            Person.StaticPrototype = new Person.PrototypeClass();
            Soldier.StaticPrototype = new Soldier.PrototypeClass();
        }

    	[Test]
        public void ExperementalTest()
        {
            Person p = new Person();
            p.Kill();
            var isAlive = p.IsAlive;
            p.PersonPrototype.set_Name = delegate(Person self, string name)
                                             {
                                                 Assert.That(self, Is.SameAs(p));
                                                 Assert.That(name, Is.EqualTo("ABC"));
                                                 self.xName = name;
                                             };
            p.Name = "ABC";
            Assert.That(p.Name, Is.EqualTo("ABC"));

            Soldier s = new Soldier();
            s.Rank = Rank.Captain;
            Assert.That(s.Rank, Is.EqualTo(Rank.Captain));
            Soldier.StaticPrototype.get_Rank = self => Rank.Lieutenant;
            Assert.That(s.Rank, Is.EqualTo(Rank.Lieutenant));

            Assert.That(Person.IsThereTrueLove, Is.EqualTo(false));
            Person.StaticPrototype.get_IsThereTrueLove = () => true;
            Assert.That(Person.IsThereTrueLove, Is.EqualTo(true));

        }

        [Test]
        public void InstancePrototypeCallTest()
        {
            Person p = new Person();

            p.PersonPrototype.set_Name = delegate(Person self, string name)
                                             {
                                                 Assert.That(self, Is.SameAs(p));
                                                 Assert.That(name, Is.EqualTo("ABC"));
                                                 self.xName = name;
                                             };
            p.Name = "ABC";

        }

        [Test]
        public void StaticPrototypeCallTest()
        {
            Person p = new Person();
            p.Name = "Something";
            Person.StaticPrototype.get_Name = self =>
                                                  {
                                                      Assert.That(self, Is.SameAs(p));
                                                      return "1";
                                                  };
            Assert.That(p.Name, Is.EqualTo("1"));

            p.PersonPrototype.get_Name = self => "2";
            Assert.That(p.Name, Is.EqualTo("2"));
        }

        [Test]
        public void StaticCallTest()
        {
            Assert.That(Person.IsThereTrueLove, Is.EqualTo(false));
            Person.StaticPrototype.get_IsThereTrueLove = () => true;
            Assert.That(Person.IsThereTrueLove, Is.EqualTo(true));
        }

        [Test]
        public void RealImplementationCallTest()
        {
            Person p = new Person();
            p.PersonPrototype.Kill = delegate { };
            p.Kill();

            Assert.That(p.IsAlive, Is.EqualTo(true));

            p.xKill();
            Assert.That(p.IsAlive, Is.EqualTo(false));
        }

        [Test]
        public void VirtualCallTest()
        {
            Soldier s0 = new Soldier();
            s0.Rank = Rank.Captain;
            s0.Kill();
            Assert.That(s0.Rank, Is.EqualTo(Rank.General));
            Assert.That(s0.IsAlive, Is.EqualTo(false));

            Soldier s1 = new Soldier();
            s1.Rank = Rank.Captain;
            s1.SoldierPrototype.Kill = delegate { };
            s1.Kill();
            Assert.That(s1.Rank, Is.EqualTo(Rank.Captain));
            Assert.That(s1.IsAlive, Is.EqualTo(true));

            Soldier s2 = new Soldier();
            s2.Rank = Rank.Captain;
            s2.SoldierPrototype.Kill = self =>
                                       (self as Person).xKill();
            s2.Kill();
            Assert.That(s2.Rank, Is.EqualTo(Rank.Captain));
            Assert.That(s2.IsAlive, Is.EqualTo(false));
        }

        [Test]
        public void CtorTest()
        {
            Soldier s1 = new Soldier();
            Assert.That(s1.IsAlive, Is.EqualTo(true));

            Soldier.StaticPrototype.Ctor = delegate { };
            Soldier s2 = new Soldier();
            Assert.That(s2.IsAlive, Is.EqualTo(false));

            Soldier.StaticPrototype.Ctor = self => self.xCtor();
            Soldier s3 = new Soldier();
            Assert.That(s3.IsAlive, Is.EqualTo(true));
        }

        [Test]
        public void OverloadsTest()
        {
            Assert.That(Calc.Add(1, 2), Is.EqualTo(3));
            Calc.StaticPrototype.Add_Int32_Int32 = (x, y) => 0;
            Assert.That(Calc.Add(1, 2), Is.EqualTo(0));

            Assert.That(Calc.Add(1.0, 2.0), Is.EqualTo(3.0));
            Calc.StaticPrototype.Add_Double_Double = (x, y) => 1 + x + y;
            Assert.That(Calc.Add(1.0, 2.0), Is.EqualTo(4.0));
        }

        [Test]
        public void ExceptionsTest()
        {
            Assert.That(Exceptions.CatchException(), Is.True);
            Exceptions.StaticPrototype.CatchException = () => false;
            Assert.That(Exceptions.CatchException(), Is.False);

            Exceptions.StaticPrototype = new Exceptions.PrototypeClass();
            Assert.That(Exceptions.CatchException(), Is.True);
            Exceptions.StaticPrototype.ThrowException = delegate { };
            Assert.That(Exceptions.CatchException(), Is.False);


            Exceptions.StaticPrototype.ThrowException = delegate
                                                            {
                                                                throw new ArgumentException();
                                                            };
            try
            {
                Exceptions.CatchException();
                Assert.Fail();
            }
            catch (ArgumentException)
            {
            }
        }

        [Test]
        public void PrivateCtorTest()
        {
            Civilian.StaticPrototype.Ctor = self => self.xName = "ABC";
        	var civilian = new Civilian();

            Assert.That(civilian.Name, Is.EqualTo("ABC"));

            Assert.That(new Doctor("Brain").Specialization, Is.EqualTo("Brain"));

            Doctor doctor = new Doctor();
            Assert.That(doctor.Specialization, Is.Null);
        }

        [Test]
        public void ArraysTest()
        {
            Doctor d = new Doctor("std");
            d.DoctorPrototype.AddPatients_Person = (self, p) => { };
            d.AddPatients(d);
            Assert.That(d.Patients, Is.Empty);

            bool wasCalled = false;
            d.DoctorPrototype.AddPatients_PersonArray = (self, ps) =>
                                                            {
                                                                self.xAddPatients(ps);
                                                                wasCalled = true;
                                                            };
            d.AddPatients(new[] {d});
            Assert.That(wasCalled);
        }

		[Test]
		public void CtorTests()
		{
			HaveDefaultPrivateCtor a = new HaveDefaultPrivateCtor();
			Assert.That(a.Data == "initialized by default private ctor");
			HaveDefaultPrivateCtor.StaticPrototype.Ctor = (e) => { e.Data = "initialized by custom ctor"; };
			a = new HaveDefaultPrivateCtor();
			Assert.That(a.Data == "initialized by custom ctor");

			HaveDefaultPublicCtor b = new HaveDefaultPublicCtor();
			Assert.That(b.Data == "Initialized by default public ctor");
			HaveDefaultPublicCtor.StaticPrototype.Ctor = (e) => { e.Data = "custom public ctor!"; };
			b = new HaveDefaultPublicCtor();
			Assert.That(b.Data == "custom public ctor!");

			HaveJustNonDefaultCtor c = new HaveJustNonDefaultCtor("non default!");
			Assert.That(c.Data == "non default!");
			HaveJustNonDefaultCtor.StaticPrototype.Ctor = (e) => { e.Data = "and default ctor now too!"; };
			c = new HaveJustNonDefaultCtor();
			Assert.That(c.Data == "and default ctor now too!");

			HaveNoCtorAtAll d = new HaveNoCtorAtAll();
			Assert.That(d.Data == null);
			HaveNoCtorAtAll.StaticPrototype.Ctor = (e) => { e.Data = "this is ghost ctor!!!"; };
			d = new HaveNoCtorAtAll();
			Assert.That(d.Data == "this is ghost ctor!!!");
		}
    }
}