using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using NUnit.Framework;
using Cheese;
using System.Diagnostics;


namespace Mice.Tests
{
    [TestFixture]
    class TemproraryTests
    {
        [Test]
        public void GenericsSimpleTests()
        {
            GenericStorage<int> a = new GenericStorage<int>(10);
            a.IntroduceItself("hello");
        }
    }
}
