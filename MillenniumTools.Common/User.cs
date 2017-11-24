using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MillenniumTools.Common
{
    public class User
    {
        public string Name { get; set; }

        public string Ip { get; set; }

        public string MacAddress { get; set; }

        public string PacketsRx { get; set; }

        public string PacketsTx { get; set; }

        public string Type { get; set; }
    }
}
