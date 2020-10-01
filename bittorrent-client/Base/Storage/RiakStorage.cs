using System;
using bittorrent_client.Base.Pieces;
using bittorrent_client.Base.Util;
using RiakClient;

namespace bittorrent_client.Base.Storage
{
    class RiakStorage : IPieceStorage
    {
        private static string ClusterConfig { get; } = "riakConfig";

        private IRiakEndPoint _cluster;
        private IRiakClient _client;

        public RiakStorage(byte[] infoHash) {
            _cluster = RiakCluster.FromConfig(ClusterConfig);
            _client = _cluster.CreateClient();
        }

        public Piece CreateNew(int idx) {
            throw new NotImplementedException();
        }

        public Piece GetPiece(int idx) {
            throw new NotImplementedException();
        }

        public bool Contains(int idx) {
            throw new NotImplementedException();
        }

        public int Count() {
            throw new NotImplementedException();
        }

        public bool Remove(int idx) {
            throw new NotImplementedException();
        }

        public void Dispose() {
            throw new NotImplementedException();
        }

        public BitField GetValidPieces() {
            throw new NotImplementedException();
        }
    }
}
