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

		//public A makeMeA<A>(A item)
		//{
		//    return item;
		//}

		//public B makeMeB<B>(B item)
		//{
		//    return makeMeA<B>(item);
		//}

		public M MakeMeSomeM<M,W>(M param, W param2)
		{
		    return param;
		}

		public L MakeSomeL<L,H>(L param, H param2)
		{
			Type[] key = {typeof(L),typeof(H)};
			if (testSample.Dict.ContainsKey(key))
		    {
				return ((Test.Maker<L,H>)testSample.Dict[key]).Invoke(this, param, param2);
		    }
			else 
			if (GenericStorage<T>.testSampleStatic.Dict.ContainsKey(key))
			{
				return ((Test.Maker<L, H>)testSampleStatic.Dict[key]).Invoke(this, param, param2);
			}
			return MakeMeSomeM<L,H>(param, param2);
		}
		
		public struct Test
		{
		    public Dictionary<Type[], object> Dict;
		    public delegate K Maker<K,J>(GenericStorage<T> self, K item, J item2);
		}

		public static Test testSampleStatic = new GenericStorage<T>.Test();
		public Test testSample = new GenericStorage<T>.Test();
    }
}
