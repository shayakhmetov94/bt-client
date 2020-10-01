using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Diagnostics;
using bittorrent_client.Base.Util;

namespace bittorrent_client.Base.Strategies
{
    class RandomPieceRqStrategy : IRequestStrategy
    {
        private int _piecesCount;
        private Stack<int> _remainingPieces;

        public RandomPieceRqStrategy(int piecesCount, BitField piecesHave = null) {
            _piecesCount = piecesCount;
            List<int> pieces = new List<int>(piecesCount);

            for (int i = 0; i < piecesCount; i++) {
                if(piecesHave != null && piecesHave[i]) {
                    continue;
                }

                pieces.Add(i);
            }

            FisherYatesShuffle(pieces, piecesCount);

            _remainingPieces = new Stack<int>(pieces);
        }

        private void FisherYatesShuffle(List<int> pieces, int piecesCount) {
            Random rnd = new Random();
            int max = piecesCount -1, r = rnd.Next(0, piecesCount), t;
            do {
                t = pieces[max];
                pieces[max] = pieces[r];
                pieces[r] = t;

                max--;
                r = rnd.Next(0, piecesCount);
            } while (max > 0);
        }

        public int Next() {
            if(_remainingPieces.Count == 0) {
                return -1;
            }

            return _remainingPieces.Pop();
        }

        public void Reset(int pos) {
            if (pos < 0 || pos >= _piecesCount) {
                throw new ArgumentOutOfRangeException("pos");
            }

            _remainingPieces.Push(pos);
        }
    }
}
