using System;
using System.Collections.Generic;
using System.Text;

namespace bittorrent_service.Models
{
    public class SpeedStamp
    {
        public DateTime UtcTime { get; set; }
        public double Value { get; set; }
    }
}
