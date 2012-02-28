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
    internal class GenericsTests
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
            Assert.That(strs.All(s => s == "Hello, i am System.Int32"));
        }

        [Test]
        public void InstancePrototypeCallTest()
        {
            GenericStorage<string> a = new GenericStorage<string>("test_failed");
            a.GenericStorage_1Prototype.get_Data = (self) => "test_completed";
            Assert.That(a.Data == "test_completed");

			GenericStorage<int> b = new GenericStorage<int>(10);
			GenericStorage<int>.StaticPrototype.IntroduceItselfStatic = () => "static_test_completed";
			Assert.That(GenericStorage<int>.IntroduceItselfStatic() == "static_test_completed");
        }

        [Test]
        public void StaticPrototypeCallTest()
        {
            GenericStorage<int>.StaticPrototype.IntroduceItself = (self) => { return "test"; };
            GenericStorage<int> a = new GenericStorage<int>(10);
        	Assert.That(a.IntroduceItself() == "test");
        }

        [Test]
        public void CtorTest()
        {
            GenericStorage<string>.StaticPrototype.Ctor_T = (self, value) =>
                                                                {
                                                                    self.Info = "cerated_by_test";
                                                                    self.xCtor(value);
                                                                };
            var a = new GenericStorage<string>("abc");
            Assert.That(a.Info == "cerated_by_test");
            Assert.That(a.Data == "abc");
        }

        [Test]
        public void GenericInGenericTest()
        {
            GenericStorage<List<int>> TestLists = new GenericStorage<List<int>>();
            TestLists.GenericStorage_1Prototype.get_Data = (self) => { return new List<int> {1, 2, 3}; };
            Assert.That(TestLists.Data.ToArray().All(e => e == TestLists.Data.IndexOf(e) + 1));
        }

        [Test]
        public void MicedGenericInGenericTest()
        {
            GenericStorage<GenericStorage<string>> a =
                new GenericStorage<GenericStorage<string>>(new GenericStorage<string>("Something to do here..."));
            a.Data.GenericStorage_1Prototype.get_Data = (self) => "nothing to do here!";
            Assert.That(a.Data.Data == "nothing to do here!");
        }

		[Test]
		public void GenericMethodTest()
		{
			GenericStorage<int> test = new GenericStorage<int>();

			//looks ugly, but it works!
			GenericStorage<int>.PrototypeClass.Callback_MeAnd2Objects_2<string, int> b = (x, y, z) => { return "test"; };
			
			//TODO:initialize it in PrototypeType's ctor
			test.GenericStorage_1Prototype.MeAnd2Objects_2 = new Dictionary<Type, object>();
			
			test.GenericStorage_1Prototype.MeAnd2Objects_2.Add(typeof(Func<string, int>), b);
			
			Assert.That(test.MeAnd2Objects<string, int>(10,"a")=="test");
		}

		[Test]
		public void GenericMethodInStaticProtoTest()
		{
			GenericStorage<int>.PrototypeClass.Callback_MeAnd2Objects_2<string, int> b = (x, y, z) => { return "test"; };
			GenericStorage<int>.StaticPrototype.MeAnd2Objects_2 = new Dictionary<Type, object>();
			GenericStorage<int>.StaticPrototype.MeAnd2Objects_2.Add((typeof(Func<string, int>)), b);

			GenericStorage<int> test = new GenericStorage<int>(10);
			test.GenericStorage_1Prototype.MeAnd2Objects_2 = new Dictionary<Type, object>();

			Assert.That(test.MeAnd2Objects<string, int>(10, "a") == "test");
		}

		[Test]
		public void Xtest()
		{
			GenericStorage<int>.PrototypeClass.Callback_MakeMeSome_2<int, string> a = (x, y, z) => { return 10; };

			GenericStorage<int>.StaticPrototype = new GenericStorage<int>.PrototypeClass();
			GenericStorage<int>.StaticPrototype.MakeMeSome_2 = new Dictionary<Type, object>();
			GenericStorage<int>.StaticPrototype.MakeMeSome_2.Add(typeof(Func<int,string>), a);

			GenericStorage<int> test = new GenericStorage<int>();
			test.GenericStorage_1Prototype = new GenericStorage<int>.PrototypeClass();
			test.GenericStorage_1Prototype.MakeMeSome_2 = new Dictionary<Type, object>();
			test.GenericStorage_1Prototype.MakeMeSome_2.Add(typeof(Func<int, string>), a);

			Assert.That(test.MakeMeSome<int,string>("s",10)==10);
		}
    }
}
