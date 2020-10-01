using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace bittorrent_service.Models
{
    public class Session
    {
        public long Id { get; set; }
        public string Title { get; set; }
        public bool isActive { get; set; }
        public string Path { get; set; }
        public IEnumerable<byte> PiecesHave { get; set; }
        public IEnumerable<byte> Metainfo { get; set; }
        public DateTime LastValidated { get; set; }
    }
}
