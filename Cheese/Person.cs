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

		public void WriteDefault4<T>(T param) where T: class, new()
		{
			
		}

		public void WriteDefault5<T>(T param)
		{

		}

		public void xxxWriteDefault5<T>(T param)
		{
			if(like_a_proto.WriteDefault4_1.ContainsKey(typeof(T)))
			{
				((Proto.Callback_WriteDefault4_1<T>)like_a_proto.WriteDefault4_1[typeof(T)]).Invoke(this,param);
			} 
			else
			{
				WriteDefault5(param);
			}
		}

		public Proto like_a_proto = new Proto();

		public class Proto
		{
			public delegate void Callback_WriteDefault4_1<T>(Person self, T param);

			private Dictionary<Type, object> _WriteDefault4_1;
			internal Dictionary<Type, object> WriteDefault4_1
			{
				get
				{
					if (this._WriteDefault4_1 == null)
						this._WriteDefault4_1 = new Dictionary<Type, object>();
					return this._WriteDefault4_1;
				}
				set
				{
					this._WriteDefault4_1 = value;
				}
			}

			public void SetWriteDefault4_1<T>(Callback_WriteDefault4_1<T> code)
			{
				this.WriteDefault4_1.Add(typeof(Func<T>), (object)code);
			}
		}

	}
}
