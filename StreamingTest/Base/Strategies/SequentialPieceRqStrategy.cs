using bittorrent_client.Base.Util;
using System;

namespace bittorrent_client.Base.Strategies
{   
    class SequentialPieceRqStrategy : IRequestStrategy
    {
        private int _piecesCount;
        protected BitField _pieces;
        
        public SequentialPieceRqStrategy(int piecesCount) {
            _piecesCount = piecesCount;
            _pieces = new BitField(piecesCount);    
        }

        public int Next() {
            int next = _pieces.FirstFalse();
            if (next < 0) {
                return -1;
            }

            _pieces[next] = true;
            return next;
        }

        public void Reset(int pos) {
            if (pos < 0 || pos >= _piecesCount) {
                throw new ArgumentOutOfRangeException("pos");
            }

            _pieces[pos] = false; 
        }
    }
}
