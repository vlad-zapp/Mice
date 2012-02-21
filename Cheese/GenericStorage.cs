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

		public M MakeMeSomeM<M>(M param)
		{
			return param;
		}

    	public struct nestedStruct
		{
			private Dictionary<Type,object> _a;
			public Dictionary<Type, object> a
			{
				get { return _a; }
				set { _a = value; }
			}

    		public delegate F makeF<F>();
		}


    }
}
