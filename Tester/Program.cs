using MillenniumTools.Common;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Tester
{
    class Program
    {
        static void Main(string[] args)
        {
            /*
            var router = new Router("192.168.1.1");
            if (router.Authenticate("admin","marco")==null)
            {
                var state = router.GetState();
                if (state.Accessible && state.Authenticated)
                {
                    router.RestartModem();
                }
            }
            */
            var ext = new Extender("192.168.1.93");
            if (ext.Authenticate("marco", "marco") == null)
            {
                var state = ext.GetState();
                if (state.Accessible && state.Authenticated)
                {
                    ext.Restart();
                }
            }
            
        }
    }
}
