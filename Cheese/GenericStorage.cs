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

		public K MakeMeSome<K, J>(J arg, K arg2)
		{
			return arg2;
		}

		public M MakeMeSomeM<M, W>(M param, W param2)
		{
			return param;
		}

		public class NestedSurprise
		{
			 public static string GiveMeLove(int times)
			 {
			 	string ret ="";
				for (int i = 0; i < times; i++) ret += "Love";
			 	return ret;
			 }

			public GenericStorage<T> MakeMyParentSample()
			{
				return new GenericStorage<T>(default(T));
			}

			public string Mystery { set; private get; }

			public string TellASecret()
			{
				return Mystery;
			}
		}

		//public L MakeSomeL<L, H>(L param, H param2)
		//{
		//    Type key = typeof(Func<L,H>);
		//    if (testSample.DictAccesor.ContainsKey(key))
		//    {
		//        return ((Test.Maker<L, H>)testSample.DictAccesor[key]).Invoke(this, param, param2);
		//    }
		//    else if (GenericStorage<T>.testSampleStatic.DictAccesor.ContainsKey(key))
		//    {
		//        return ((Test.Maker<L, H>)testSampleStatic.DictAccesor[key]).Invoke(this, param, param2);
		//    }
		//    return MakeMeSomeM<L, H>(param, param2);
		//}

		//public struct Test
		//{
		//    public Dictionary<Type, object> Dict;
		//    public Dictionary<Type, object> DictAccesor { 
		//        get
		//        {
		//            if(Dict==null)
		//            {
		//                Dict=new Dictionary<Type, object>();
		//            }
		//            return Dict;
		//        }
		//        set
		//        {
		//            Dict = value;
		//        }
		//    }
		//    public delegate K Maker<K, J>(GenericStorage<T> self, K item, J item2);

		//    public void SetMakeSomeL<L,H>(Maker<L,H> target)
		//    {
		//        DictAccesor.Add(typeof(Func<L,H>),target);
		//    }
		//}

		//public static Test testSampleStatic = new GenericStorage<T>.Test();
		//public Test testSample = new GenericStorage<T>.Test();
	}
}
