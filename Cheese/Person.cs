using System;
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

		public virtual void Kill()
		{
			_isAlive = false;
		}

		public bool WriteDefault<T>(out T param)
		{
			//param = default(T);
			param = default(T);
			return true;
		}

		public bool WriteDefault2<T>(out T param)
		{
			return WriteDefault(out param);
		}

		public bool WriteDefault3<T>(T param)
		{
			return true;
		}

	}
}
