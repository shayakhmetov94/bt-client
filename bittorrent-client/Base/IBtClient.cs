using bittorrent_client.Base.Pieces;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;

namespace bittorrent_client.Base
{
    /// <summary>
    /// BitTorrent counters structure required by announcer
    /// </summary>
    public struct BtClientStats {
        public long Downloaded;
        public long Left;
        public long Uploaded;
    }

    public interface IBtClient
    {
        /// <summary>
        /// Must return client's statistics
        //// </summary>
        /// <returns>BtClientStats</returns>
        BtClientStats GetStats();

        /// <summary>
        /// Must return client's listening endpoint
        /// </summary>
        /// <returns>Client's listening endpoint</returns>
        IPEndPoint GetEndPoint();

        /// <summary>
        /// Must return client's info hash
        /// </summary>
        /// <returns>Client's info hash</returns>
        byte[] GetInfoHash();

        /// <summary>
        /// Must return peer id of client
        /// </summary>
        /// <returns>Peer id of client</returns>
        byte[] GetPeerID();

        /// <summary>
        /// Must create new piece in accordance with client's piece block length and piece length
        /// </summary>
        /// <returns>New data piece</returns>
        Piece GetPiece(int idx);
    }
}
