using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Cheese;
using System.Diagnostics;


namespace Mice.Tests
{
    [TestFixture]
    class GenericsTests
    {
        [Test]
        public void SimplePrototypeCallTest()
        {

            GenericStorage<int> a = new GenericStorage<int>(10);
            string[] strs = {
                           a.IntroduceItself(),
                           a.IntroduceItself("Hello, i am"),
                           a.xIntroduceItself(),
                           a.xIntroduceItself("Hello, i am")
                       };
            Assert.That(strs.All(s=>s=="Hello, i am System.Int32"));
        }

        [Test]
        public void InstancePrototypeCallTest()
        {
            GenericStorage<string> a = new GenericStorage<string>("test_failed");
            a.GenericStorage_1Prototype.get_Data = (self) => "test_completed";
            Assert.That(a.Data=="test_completed");

            GenericStorage<int> b = new GenericStorage<int>(10);
            GenericStorage<int>.StaticPrototype.IntroduceItselfStatic = () => "static_test_completed";
            Assert.That(GenericStorage<int>.IntroduceItselfStatic() == "static_test_completed");
        }
    }
}
