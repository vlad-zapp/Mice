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

        public GenericStorage(T initial_value)
        {
            Data = initial_value;
        }

        public string IntroduceItself()
        {
            return String.Format("Hello, i am {0}", typeof(T).ToString());
        }

        public string IntroduceItself(string greeting)
        {
            return String.Format("{1} {0}", greeting, typeof(T).ToString());
        }

        public static string IntroduceItselfStatic()
        {
            return "Hi i am just a static method";
        }

        public struct InnerStruct
        {
            private int a;
            private string b;
            private float c;

            public delegate string IntroduceItself(InnerStruct self);
            public IntroduceItself IntroduceItselfImpl;
        }

        public static InnerStruct InnerStructStaticSample ;
    }
}
