using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace bittorrent_service.Models
{
    public class Stats
    {
        public long SessionCount { get; set; }
        public long ActiveSessionCount { get; set; }        
    }
}
