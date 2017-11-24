using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MillenniumTools.Common
{
    public class ThroughputResult
    {
        public long Transmit { get; set; }
        public long Receive { get; set; }

        public DateTime Time { get; set; }
        public TimeSpan UpTime { get; set; }
    }
}
