using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bittorrent_client.Base
{
    public class InfoHash
    {
        public byte[] Raw { get; }
        public string AsString { 
            get {
                if(_infoHashAsString == null) {
                    _infoHashAsString = Encoding.ASCII.GetString(Raw);
                }

                return _infoHashAsString;
            } 
        }

        private string _infoHashAsString = null;

        public InfoHash(byte[] raw) {
            Raw = raw;
        }
    }
}
