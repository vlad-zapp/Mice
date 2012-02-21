using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Cheese;
using NUnit.Framework;

namespace Mice.Tests
{
	[TestFixture]
	class TemproraryTests
	{
		[Test]
		public void GenericMethodTest()
		{
			GenericStorage<string> a = new GenericStorage<string>("generic_method_test");
			GenericStorage<string>.StaticPrototype.MakeItString_S.Add(typeof(StringBuilder),"");
			a.GenericStorage_1Prototype.MakeItString_S.Add(typeof (StringBuilder), "");
		}
	}
}
