using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace test
{
    public class tested
    {
        public bool say()
        {
            Exception ex = new Exception("this test always throws an exception");
            throw ex;
        }
    }
}
