using bittorrent_client.Base;
using bittorrent_client.Base.Pieces;
using System.Net;

namespace Bittorrent_tests
{
    class TestBtClient : IBtClient
    {
        public IPEndPoint GetEndPoint() {
            return new IPEndPoint(IPAddress.Loopback, 44444);
        }

        public byte[] GetInfoHash() {
            return new byte[20];
        }

        public byte[] GetPeerID() {
            return new byte[20];
        }

        public Piece GetPiece(int idx) {
            throw new System.NotImplementedException();
        }

        public BtClientStats GetStats() {
            return new BtClientStats()
            {
                Downloaded  = 32,
                Left        = 64,
                Uploaded    = 128
            };
        }
    }
}
