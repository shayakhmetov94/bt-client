using bittorrent_service.Db;
using System;
using System.Collections.Concurrent;
using System.Data;

namespace bittorrent_service
{
    public class BtService
    {
        private static BtService _instance;

        public static BtService GetInstance() {
            return _instance;
        }

        public static BtService GetInstance(ISqlDbConnectionFactory connProvider) {
            if (_instance == null) {
                if (connProvider == null) {
                    throw new ArgumentNullException(nameof(connProvider));
                }

                _instance = new BtService(connProvider);
            }

            return _instance;
        }

        private ISqlDbConnectionFactory _connProvider;
        private ConcurrentDictionary<long, TorrentSession> _torrents;

        BtService(ISqlDbConnectionFactory connProvider) {
            _connProvider = connProvider;
        }

        public bool Add(TorrentSession torrentInfo) {
            using(IDbConnection dbConnection = _connProvider.GetConnection()) {
                 //dbConnection.
            }

            return false;
        }

        public bool Remove(TorrentSession torrentInfo) {
         
            return false;
        }
    }
}
