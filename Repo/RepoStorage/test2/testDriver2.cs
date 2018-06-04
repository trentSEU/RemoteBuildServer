using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace test
{
    public interface ITest
    {
        bool test();
    }

    public class TestDriver : ITest
    {
        public bool test()
        {
            tested t = new tested();
            return t.say();
        }
    }

}

