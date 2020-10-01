using bittorrent_client.Base.Pieces;
using bittorrent_client.Base.Util;
using System;

namespace bittorrent_client.Base.Storage
{
    public interface IPieceStorage : IDisposable
    {
        bool Contains(int idx);
        Piece GetPiece(int idx);
        BitField GetValidPieces();
        Piece CreateNew(int idx);
        int Count();
        bool Remove(int idx);
    }
}
