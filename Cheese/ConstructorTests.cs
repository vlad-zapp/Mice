using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cheese
{
	public class HaveNoCtorAtAll
	{
		public string Data;
	}

	public class HaveDefaultPrivateCtor : HaveNoCtorAtAll
	{
		private HaveDefaultPrivateCtor()
		{
			Data = "initialized by default private ctor";
		}
	}

	public class HaveDefaultPublicCtor : HaveNoCtorAtAll
	{
		public HaveDefaultPublicCtor()
		{
			Data = "Initialized by default public ctor";
		}
	}

	public class HaveJustNonDefaultCtor : HaveNoCtorAtAll
	{
		public HaveJustNonDefaultCtor(string init)
		{
			Data = init;
		}
	}
}
