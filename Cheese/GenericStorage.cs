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

		//public static string IntroduceItselfStatic()
		//{
		//    return "Hi i am just a static method";
		//}

		//public A makeMeA<A>(A item)
		//{
		//    return item;
		//}

		//public B makeMeB<B>(B item)
		//{
		//    return makeMeA<B>(item);
		//}

		public M MakeMeSomeM<M>(M param)
		{
			return param;
		}

		public L MakeSomeL<L>(L param)
		{
			if (testSample.Dict[typeof(L)] != null)
			{
				return ((Test.Maker<L>)testSample.Dict[typeof(L)]).Invoke(this, param);
			}
			return MakeMeSomeM<L>(param);
		}
		
    	public struct Test
    	{
			public Dictionary<Type, object> Dict;
			public delegate K Maker<K>(GenericStorage<T> self, K item);
    	}

    	public Test testSample = new GenericStorage<T>.Test();
    }
}
