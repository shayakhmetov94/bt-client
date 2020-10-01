using bittorrent_client.Base.Util;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bittorrent_client.Base.Strategies
{
    public class RarestFirstRqStrategy : IRequestStrategy
    {
        private readonly int InitialRandomPiecesCount = 4;
        private object __remainingPiecesLock = new object();

        private int _piecesCount;
        private Stack<int> _remainingPieces;
        private Stack<int> _randomPieces;
        private BitField _requested;

        public RarestFirstRqStrategy(int piecesCount, BitField piecesHave = null) {
            _piecesCount = piecesCount;
            _requested = piecesHave == null ? new BitField(_piecesCount) : piecesHave;
            _randomPieces = new Stack<int>(InitialRandomPiecesCount);

            int falseCount = piecesCount - _requested.TrueCount;
            int randomPiecesCount = falseCount < InitialRandomPiecesCount ? falseCount : InitialRandomPiecesCount;

            //Random pieces before rarest first algorithm
            if (randomPiecesCount > 0) {
                int[] falseIndicies = new int[falseCount];
                BitField requestedCopy = _requested.Clone();
                for (int i = 0; i < falseCount; i++) {
                    int falseIdx = requestedCopy.FirstFalse();
                    falseIndicies[i] = falseIdx;
                    requestedCopy[falseIdx] = true;
                }

                FisherYatesShuffle(falseIndicies, falseCount);
                for (int i = 0; i < randomPiecesCount; i++) {
                    _randomPieces.Push(falseIndicies[i]);
                }
            }
        }

        public void UpdateRarity(List<double> piecesFrequency) {
            if (piecesFrequency == null) {
                throw new ArgumentNullException("piecesFrequency");
            }

            lock (__remainingPiecesLock) {
                int i = 0;
                _remainingPieces = new Stack<int>(
                    piecesFrequency
                        .Select((w) => new Tuple<int, double>(i++, w))
                        .Where((t) => !_requested[t.Item1])
                        .OrderBy((t) => t.Item2)
                        .Select((t) => t.Item1)
                );
            }
        }

        public int Next() {
            if (_randomPieces.Count > 0) {
                return _randomPieces.Pop();
            }

            lock (__remainingPiecesLock) {

                if (_remainingPieces == null || _remainingPieces.Count == 0) {
                    return -1;
                }

                int piece = _remainingPieces.Pop();
                _requested[piece] = true;
                return piece;
            }
        }

        public void Reset(int pos) {
            if (pos < 0 || pos >= _piecesCount) {
                throw new ArgumentOutOfRangeException("pos");
            }

            lock (__remainingPiecesLock) {
                _requested[pos] = false;
                _remainingPieces.Push(pos);
            }
        }

        private void FisherYatesShuffle(int[] pieces, int piecesCount) {
            Random rnd = new Random();
            int max = piecesCount - 1, r = rnd.Next(0, piecesCount), t;
            do {
                t = pieces[max];
                pieces[max] = pieces[r];
                pieces[r] = t;

                max--;
                r = rnd.Next(0, piecesCount);
            } while (max > 0);
        }
    }
}
