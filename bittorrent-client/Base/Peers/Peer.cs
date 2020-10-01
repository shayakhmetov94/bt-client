using System;
using System.Net;
using System.Threading;

namespace bittorrent_client.Base.Peers
{
    public class Peer
    {
        #region Поля, свойства и константны
        
        /// <summary>
        /// Peer's id
        /// </summary>
        public string PeerId { get; }

        /// <summary>
        /// Peer's address
        /// </summary>
        public IPAddress IpAddress { get; }

        /// <summary>
        /// Peer's port
        /// </summary>
        public int Port { get; }

        /// <summary>
        /// Is peer choking us
        /// </summary>
        private int _isChoking = 1;
        public bool IsChoking {
            get { return _isChoking > 0; }
            set {
                int valAsInt32 = value ? 1 : 0;
                Interlocked.Exchange(ref _isChoking, valAsInt32);
            }
        }

        /// <summary>
        /// Are we choking peer
        /// </summary>
        private int _isChoked = 1;
        public bool IsChoked {
            get { return _isChoked > 0; }
            set {
                int valAsInt32 = value ? 1 : 0;
                Interlocked.Exchange(ref _isChoked, valAsInt32);
            }
        }

        /// <summary>
        /// Are we interested in peers offerings
        /// </summary>
        public bool IsClientInterested { get; set; }

        /// <summary>
        /// Is peer interested in our offerings
        /// </summary>
        public bool IsInterested { get; set; }

        #endregion

        public Peer(IPAddress ipAddress, int port, string peerId = null) {
            IpAddress = ipAddress;
            Port = port;
        }

        public Peer(string adress, int port, string peerId = null) {
            IpAddress = IPAddress.Parse(adress);
            Port = port;
        }

        public Peer(byte[] peerInfo) {
            var ipv4 = new byte[4];
            Buffer.BlockCopy(peerInfo, 0, ipv4, 0, 4);
            IpAddress = new IPAddress(ipv4);
            Port = 0;
            Port |= peerInfo[4];
            Port <<= 8;
            Port |= peerInfo[5];
        }

        public override int GetHashCode() {
            return IpAddress.GetHashCode() ^ Port;
        }

        public override bool Equals(object obj) {
            if (obj is Peer) {
                Peer peer = (Peer)obj;
                return IpAddress.Equals(peer.IpAddress) && Port.Equals(peer.Port);
            }

            return base.Equals(obj);
        }
    }
}