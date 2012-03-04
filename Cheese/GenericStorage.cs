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
	}
}
