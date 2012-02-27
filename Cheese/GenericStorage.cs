using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cheese
{
	/// <summary>
	/// Just a container for any type variable.
	/// </summary>
	/// <typeparam name="T">type of inner data</typeparam>
	public class GenericStorage<T>
	{
		public T Data { get; private set; }
		public string Info;

		public GenericStorage(T initial_value)
		{
			Data = initial_value;
		}

		public string Greeting()
		{
			return IntroduceItself();
		}

		public string IntroduceItself()
		{
			return IntroduceItself("Hello, i am");
		}

		public string IntroduceItself(string greeting)
		{
			return String.Format("{0} {1}", greeting, typeof(T).ToString());
		}

		public static string IntroduceItselfStatic()
		{
			return "Hi i am just a static method";
		}

		public static string MakeItString<L>(L obj)
		{
			return obj.ToString();
		}

		//public static string MakeMe2Strings<L, M>(L obj, M obj2)
		//{
		//    return obj.ToString() + obj2.ToString();
		//}

		public string MeAnd2Objects<L,M>(M obj, L obj2)
		{
		    return this.ToString() + obj.ToString() + obj2.ToString();
		}

		//public K MakeMeSome<K,J>(J arg, K arg2)
		//{
		//    return arg2;
		//}

		public string MakeMeSomeM<M, W>(M param, W param2)
		{
			return "hi, i am makesomem";
		}

		public string MakeSomeL<L, H>(L param, H param2)
		{
			Type key = typeof(Func<L,H>);
			if (testSample.Dict.ContainsKey(key))
			{
				return ((Test.Maker<L, H>)testSample.Dict[key]).Invoke(this, param, param2);
			}
			else if (GenericStorage<T>.testSampleStatic.Dict.ContainsKey(key))
			{
				return ((Test.Maker<L, H>)testSampleStatic.Dict[key]).Invoke(this, param, param2);
			}
			return MakeMeSomeM<L, H>(param, param2);
		}

		public struct Test
		{
			public Dictionary<Type, object> Dict;
			public delegate string Maker<K, J>(GenericStorage<T> self, K item, J item2);
			public string _strin;
		}

		public static Test testSampleStatic = new GenericStorage<T>.Test();
		public Test testSample = new GenericStorage<T>.Test();
	}
}
