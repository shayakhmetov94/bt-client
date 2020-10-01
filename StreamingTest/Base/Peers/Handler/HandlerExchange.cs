using bittorrent_client.Base.Util;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using bittorrent_client.Base.Strategies;
using System.Collections.Generic;
using FluentScheduler;

namespace bittorrent_client.Base.Peers.Handler
{
    public class HandlerExchange: IResourcePool<PeerHandler>
    {
        public const int MaxSize = 4;
        public const int OptimisticUcnhokeInSecs = 30;
        public const int DownloaderPoolRefreshInSecs = 10;

        public Func<PeerHandler, double> RateSelector { get; set; } = (h) => h.DownloadRate;

        private ConnectionHub _conMan;
        public PeerStateCache _stateCache;
        private BlockingCollection<PeerHandler> _uploaderPool;
        private CancellationTokenSource _myCancelTokenOwner;
        private PeerHandler _luckyHandler;
        private ConcurrentDictionary<string, PeerHandler> _curPoolUploaders;
        private ConcurrentDictionary<string, PeerHandler> _curPoolDownloaders;

        private Task _acuqireNewHandlerTask;

        private long _isDownloaderRefreshRunning = 0;

        public HandlerExchange(ConnectionHub connMan, PeerStateCache stateCache) {
            _conMan = connMan;
            _stateCache = stateCache;
            _uploaderPool = new BlockingCollection<PeerHandler>();
            _myCancelTokenOwner = new CancellationTokenSource();

            _curPoolUploaders = new ConcurrentDictionary<string, PeerHandler>();
            _curPoolDownloaders = new ConcurrentDictionary<string, PeerHandler>();
            
            _acuqireNewHandlerTask = null;

            JobManager.AddJob(OptimisticUnchoke, (s) => s.ToRunEvery(OptimisticUcnhokeInSecs).Seconds());
            JobManager.AddJob(TryRefreshDownloaderPool, (s) => s.ToRunEvery(DownloaderPoolRefreshInSecs).Seconds());
        }

        private void TryRefreshDownloaderPool() {
            if(Interlocked.Read(ref _isDownloaderRefreshRunning) == 0 && _uploaderPool.Count > 0) {
                RefreshDownloaderPool();
            }
        }
        
        private void RefreshDownloaderPool() {
            Interlocked.Increment(ref _isDownloaderRefreshRunning);
            var handler = _conMan.Acquire(_myCancelTokenOwner.Token);
            if(handler == null) {
                return;
            }
            var worst = GetWorstDownloader(RateSelector);
            if(worst != null && RateSelector(worst) < RateSelector(handler)) {
                if(handler.Peer.IsInterested) {
                    OnPeerInterested(handler);
                } else {
                    handler.OnInterested += OnPeerInterested;
                }

                handler.SendChoke(false);
            }

            _conMan.Realese(handler);
            Interlocked.Decrement(ref _isDownloaderRefreshRunning);
        }

        private void OnPeerInterested(PeerHandler handler) {
            if(ChokeWorstDownloader(RateSelector, RateSelector(handler))) {
                AddDownloader(handler);
            }
        }

        public IEnumerable<PeerHandler> GetUploaders() {
            return _curPoolUploaders.Select((kv) => kv.Value).ToList();
        }

        public PeerHandler Acquire(int pieceIdx) {
            do {
                TryAcquireForPiece(pieceIdx, _myCancelTokenOwner.Token);

                var handler = _uploaderPool.Take(_myCancelTokenOwner.Token);

                if(!handler.PiecesHave[pieceIdx]) {
                    _conMan.Realese(handler);
                } else {
                    return handler;
                }
            } while(!_myCancelTokenOwner.Token.IsCancellationRequested);
            
            return null;
        }

        public PeerHandler Acquire(int pieceIdx, CancellationToken cancelToken) {
            do {
                TryAcquireForPiece(pieceIdx, cancelToken);

                var handler = _uploaderPool.Take(cancelToken);

                if(handler.PiecesHave[pieceIdx]) {
                     return handler;
                } else {
                    _conMan.Dispose(handler);
                }
            } while(!cancelToken.IsCancellationRequested);

            return null;
        }

        private void TryAcquireForPiece(int pieceIdx, CancellationToken cancelToken) {
            if(_acuqireNewHandlerTask == null || _acuqireNewHandlerTask.Status != TaskStatus.Running) {
                _acuqireNewHandlerTask = Task.Factory.StartNew(TryAcquireForPieceObj, new Tuple<int, CancellationToken>(pieceIdx, cancelToken));
            }
        }

        private void TryAcquireForPieceObj(object pieceIdxNcancelTknTupleObj) {
            Tuple<int, CancellationToken> pieceIdxNcancelTknTuple = (Tuple<int, CancellationToken>)pieceIdxNcancelTknTupleObj;
            AcquireForPiece(pieceIdxNcancelTknTuple.Item1, pieceIdxNcancelTknTuple.Item2);
        }

        private void AcquireForPiece(int pieceIdx, CancellationToken cancelToken) {
            AcquireNewForPiece(pieceIdx, cancelToken);
        }

        private void AcquireNewForPiece(int pieceIdx, CancellationToken cancelToken) {
            while(cancelToken.IsCancellationRequested) {
                try {
                    var handler = _conMan.Acquire(cancelToken);

                    if(handler == null) {
                        break;
                    }

                    if(handler.PiecesHave[pieceIdx]) {
                        if(handler.Peer.IsChoking) {
                            handler.SendInterested(true);
                            _stateCache.AwaitUnchoke(handler, this, 30);
                            Trace.WriteLine($"HandlerExchange: awaiting unchoke from {handler.PeerIp}");
                        } else {
                            _uploaderPool.Add(handler);
                        }
                    } else {
                        Debug.WriteLine($"HandlerExchange: returning {handler.PeerIp} because it does not have piece #{pieceIdx}");

                        _stateCache.WaitAndRealese(handler, this, 10);
                    }

                } catch(OperationCanceledException) {
                    return;
                }
            }
        }

        public void Dispose(PeerHandler resource) {
            _conMan.Dispose(resource);
            _curPoolUploaders.TryRemove(resource.PeerIp, out PeerHandler removed);
            _curPoolDownloaders.TryRemove(resource.PeerIp, out removed);
        }

        public void Realese(PeerHandler resource) {
            try {
                if(resource.Connection.Connected) {
                    if(resource.Peer.IsChoking && resource.Peer.IsChoked) {
                        Dispose(resource);
                        return;
                    }

                    RegisterUploader(resource);
                    _uploaderPool.Add(resource);
                    return;
                }
            } catch { }

            Dispose(resource);
        }

        public void AddDownloader(PeerHandler interested) {
            if(_curPoolUploaders.Count < MaxSize) {
                interested.SendChoke(false);
                RegisterDownloader(interested);
            }
        }

        private void RegisterDownloader(PeerHandler downloader) {
            _curPoolDownloaders.AddOrUpdate(downloader.PeerIp, downloader, (o, n) => n);
        }

        private void RegisterUploader(PeerHandler uploader) {
            _curPoolUploaders.AddOrUpdate(uploader.PeerIp, uploader, (o, n) => n);
        }

        private PeerHandler GetWorstDownloader(Func<PeerHandler, double> rateSelector) {
            if(_curPoolDownloaders.Count > 0) {
                var worst = _curPoolDownloaders
                               .ToList()
                               .OrderBy((h) => rateSelector(h.Value))
                               .First()
                               .Value;

                return worst;
            }

            return null;
        }

        private bool ChokeWorstDownloader(Func<PeerHandler, double> rateSelector, double toRate) {
            Debug.WriteLine("HandlerExchange: Choking worst downloader");

            PeerHandler worst = GetWorstDownloader(rateSelector);

            if(worst != null && rateSelector(worst) < toRate) {
                worst.SendChoke(true);
                Dispose(worst);
                return true;
            }

            return false;
        }

        
        private void OptimisticUnchoke() {
            if(_uploaderPool.Count== 0) {
                return;
            }

            Debug.WriteLine("HandlerExchange: Invoked optimistic unchoke");
            CancellationTokenSource optCancelSource = new CancellationTokenSource(5000);

            if(_luckyHandler != null) {
                if(ChokeWorstDownloader(RateSelector, RateSelector(_luckyHandler))) {
                    if(_luckyHandler.Peer.IsInterested) {
                        RegisterDownloader(_luckyHandler);
                    }

                    _luckyHandler.SendChoke(true);
                    Dispose(_luckyHandler);
                }
            }

            var luckyOne = _conMan.Acquire(optCancelSource.Token);
            if(luckyOne != null && luckyOne.Peer.IsChoked) {
                if(luckyOne.Peer.IsInterested) {
                    luckyOne.SendChoke(false);
                    _uploaderPool.Add(luckyOne);
                    return;
                }

                _luckyHandler = luckyOne;
                luckyOne.SendChoke(false);
                _conMan.Realese(luckyOne);
            }
        }

        public void Stop() { 
            _myCancelTokenOwner.Cancel();
            while(_uploaderPool.Count > 0) {
                var uploader = _uploaderPool.Take();
                if(uploader != null) {
                    Dispose(uploader);
                }
            }
            foreach(var downloader in _curPoolDownloaders.Values) {
                Dispose(downloader);
            }
        }

        public PeerHandler Acquire() {
            return ((IResourcePool<PeerHandler>)_conMan).Acquire();
        }

        public PeerHandler Acquire(CancellationToken token) {
            return ((IResourcePool<PeerHandler>)_conMan).Acquire(token);
        }
    }
}
