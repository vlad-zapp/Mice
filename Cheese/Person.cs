﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cheese
{
	public class Person
	{
		private static bool _isThereTrueLove;
		public static bool IsThereTrueLove
		{
			get { return _isThereTrueLove; }
			set { _isThereTrueLove = value; }
		}

		private bool _isAlive;
		public bool IsAlive
		{
			get
			{
				return _isAlive;
			}
		}

		private string _name;
		public string Name
		{
			get
			{
				return _name;
			}
			set
			{
				_name = value;
			}
		}

		public Person()
		{
			_isAlive = true;
		}

		public void Kill()
		{
			_isAlive = false;
		}

		private void TestStruct()
		{
			if (SomeNestedClassInst.o != null)
				SomeNestedClassInst.o(new object());
		}

		public static SomeNestedClass SomeNestedClassInst;
		public struct SomeNestedClass
		{
			public Action<object> o;
		}
	}
}
