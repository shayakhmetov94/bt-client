using bittorrent_client.Base.Peers.Handler;
using bittorrent_client.Base.Strategies;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace bittorrent_client.Base.Util
{
    public class PeerStateCache
    {
        private RarestFirstRqStrategy _rarestPieceRqStrat;
        private List<double> _piecesRarity { get; set; }

        public PeerStateCache(RarestFirstRqStrategy rarestPieceRqStrat = null) {
            _rarestPieceRqStrat = rarestPieceRqStrat;
        }
        
        public void AwaitUnchoke(PeerHandler handler, IResourcePool<PeerHandler> returnPool, int awaitSeconds, int triesCount = 1) {
            ManualResetEventSlim eventReset = new ManualResetEventSlim();

            void OnPeerUnchoke(PeerHandler h) {
                eventReset.Set();

                if(_rarestPieceRqStrat != null) {
                    UpdatePiecesRarity(h.PiecesHave);
                }

                h.OnUnchoke -= OnPeerUnchoke;
            }

            handler.OnUnchoke += OnPeerUnchoke;
            AwaitHandler(eventReset, handler, returnPool, awaitSeconds, triesCount);
        }

        public void AwaitBitfield(PeerHandler handler, IResourcePool<PeerHandler> returnPool, int awaitSeconds, int triesCount = 1) {
            ManualResetEventSlim eventReset = new ManualResetEventSlim();
            handler.OnBitfield += h => eventReset.Set();

            AwaitHandler(eventReset, handler, returnPool, awaitSeconds, triesCount);
        }

        public void WaitAndRealese(PeerHandler handler, IResourcePool<PeerHandler> returnPool, int waitSeconds) {
            Task.Factory.StartNew(() => {
                PeerHandler h = handler;
                Thread.Sleep(waitSeconds);
                returnPool.Realese(h);
            });
        }

        public void AwaitHandler(ManualResetEventSlim eventReset, PeerHandler handler, IResourcePool<PeerHandler> returnPool, int awaitSeconds, int triesCount = 1) {
            Task.Factory.StartNew(() => {
                PeerHandler h = handler; 
                for(int i = 0; i < triesCount; i++) {
                    if(eventReset.Wait(awaitSeconds * 1000)) {
                        returnPool.Realese(h);
                        return;
                    }
                }

                returnPool.Dispose(h);
            });
        }

        private void UpdatePiecesRarity(BitField newPieces) {
            if(_piecesRarity == null) {
                _piecesRarity = new List<double>(newPieces.Length);
                for (int i = 0; i < newPieces.Length; i++) {
                    _piecesRarity.Add(1.0);
                }
            }

            if (!newPieces.AllTrue) {
                for (int i = 0; i < newPieces.Length; i++) {
                    if (newPieces[i]) {
                        _piecesRarity[i] = _piecesRarity[i] < 1.0 ? _piecesRarity[i] * 2 : 1.0;
                    } else {
                        _piecesRarity[i] = _piecesRarity[i] / 2;
                    }
                }
            }

            _rarestPieceRqStrat.UpdateRarity(_piecesRarity);
        }
    }
}
