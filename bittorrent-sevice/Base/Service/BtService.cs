using bittorrent_client.Base;
using bittorrent_service.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bittorrent_sevice.Base.Service
{
    class BtService
    {
        public void AddSession(Metainfo metainfo) {
            if (metainfo == null) {
                throw new ArgumentNullException(nameof(metainfo));
            }


        }
         private void RegisterSession(TorrentSession session) {
            
        }

        public List<TorrentSession> GetSessions() {
            return null;
        }


        public BtService() {
            
        }


    }
}
