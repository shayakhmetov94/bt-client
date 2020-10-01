using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Bittorrent;
using bittorrent_client.Base.Pieces;
using bittorrent_client.Base.Util;

namespace bittorrent_client.Base.Storage
{
    public class MemCachedPieceStorage : IPieceStorage {
        private int _capacity;
        private ConcurrentDictionary<int, Piece> _cache;
        private ConcurrentQueue<int> _indicies;

        public int BlockSize { get; }
        public int PieceLength { get; }

        public MemCachedPieceStorage(int cacheSize, int pieceBlockSize, int pieceLen) {
            _capacity = cacheSize; 
            _cache = new ConcurrentDictionary<int, Piece>(5, cacheSize);
            _indicies = new ConcurrentQueue<int>();
            BlockSize = pieceBlockSize;
            PieceLength = pieceLen;
        }

        public bool Contains(int idx) {
            return _cache.ContainsKey(idx);
        }

        public Piece GetPiece(int idx) {
            if(_cache.ContainsKey(idx)) {
                return _cache[idx];
            }

            return CreateNew(idx);
        }

        public Piece CreateNew(int idx) {
            if (idx < 0) {
                throw new ArgumentException("idx");
            }

            Piece piece = new Piece(idx, PieceLength, BlockSize);
            piece.OnPieceDone += RemovePiece;
            Put(piece);
            return piece;
        }

        public int Count() {
            return _cache.Count;
        }

        public bool Remove(int idx) {
            return _cache.TryRemove(idx, out Piece removed);
        }

        public void Put(Piece piece) {
            if (_cache.ContainsKey(piece.Index)) {
                return;
            }
            int pieceIdx = piece.Index;
            if (_indicies.Count >= _capacity) {
                if (_indicies.TryDequeue(out int dequedIdx)) {
                    if (_cache.TryRemove(dequedIdx, out Piece removedPiece)) {
                        Debug.WriteLine($"Removed piece #{removedPiece.Index} from cache");
                        _cache[dequedIdx] = null;
                    }
                } else {
                    throw new InvalidOperationException($"Can't deque piece #{dequedIdx} from cache");
                }
            }

            _cache.AddOrUpdate(pieceIdx, piece, (o, n) => n);
            _indicies.Enqueue(pieceIdx);

        }

        public void Dispose() {
            _cache.Clear();
        }

        public BitField GetValidPieces() {
            return null;
        }

        private void RemovePiece(Piece piece) {
            Remove(piece.Index);

            piece.OnPieceDone -= RemovePiece;
        }
    }
}
