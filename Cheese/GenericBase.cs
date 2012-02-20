using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace Cheese
{
    public class GenericStuff<T>
    {
        private T _baseData;
        private GenericStuff()
        {
            
        }

        public string sayHello()
        {
            return "Hello!";
        }
    }
}
