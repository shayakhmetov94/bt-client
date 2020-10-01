using System;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;
using System.IO;
using bittorrent_client.Base.Peers.Handler;
using bittorrent_client.Base.Pieces;

namespace bittorrent_client.Base.Strategies
{
    /// <summary>
    /// Make manager self aware
    /// </summary>
    public class PiecePicker
    {
        private static readonly int WaitTimeoutForLowerPoolInMSec       = 5000;
        private static readonly int WaitTimeoutForEndGamePiecesInMSec   = 5000;

        public event Action Finished;

        private readonly object _writeLock = new object();
        private Task _pickerTask;
        private IBtClient _client;
        private HandlerExchange _exchange;
        private int _blockLen;
        private CancellationTokenSource _myCancelTokenOwner;
        private CancellationToken _outCancelToken;

        public IRequestStrategy RequestStrategy { get; private set; }

        public PiecePicker(IBtClient client, HandlerExchange exchange, IRequestStrategy requestStrategy, int blockLen, CancellationToken? cancelToken) {
            _pickerTask = new Task(StartRequesting);
            _client = client;
            _exchange = exchange;
            RequestStrategy = requestStrategy;
            _blockLen = blockLen;
            _myCancelTokenOwner = new CancellationTokenSource();
            _outCancelToken = cancelToken ?? _myCancelTokenOwner.Token;
        }

        public void Start() {
            _pickerTask.Start();
        }

        private void StartRequesting() {
            CountdownEvent pendingPiecesCountDown = new CountdownEvent(1);
            Piece nextPiece = null;
            bool stopped = false;
            do {
                do {
                    int next = RequestStrategy.Next();
                    if (next < 0) {
                        break;
                    }

                    nextPiece = _client.GetPiece(next);
                    Trace.WriteLine($"RequestManager: Next piece to request is # {next}");
                    if (nextPiece.IsValid) { // Might be valid if it was fetched from external storage
                        Debug.WriteLine($"RequestManager: Fetched piece #{nextPiece.Index} from cache");
                        continue;
                    }

                    int start = 0, curBlockLen = _blockLen;

                    pendingPiecesCountDown.AddCount();
                    nextPiece.OnPieceDone += (p) => { pendingPiecesCountDown.Signal(); };

                    do {
                        PeerHandler handler = null;

                        try {
                            Debug.WriteLine($"RequestManager: Acquiring handler...");
                            handler = _exchange.Acquire(nextPiece.Index, _outCancelToken);
                            Debug.WriteLine($"RequestManager: Handler acquired");
                        } catch (OperationCanceledException) {
                            Trace.WriteLine($"RequestManager: Cancelled acquire, restarting...");
                            break;
                        }

                        // nothing in lower pools, wait and try to acquire handler once more
                        if (handler == null) {
                            Debug.WriteLine($"RequestManager: No handler in lower pools, waiting {WaitTimeoutForLowerPoolInMSec / 1000} secs and trying once more");
                            Thread.Sleep(WaitTimeoutForLowerPoolInMSec);
                            continue;
                        }

                        curBlockLen = nextPiece.PieceLength - start < _blockLen ? nextPiece.PieceLength - start : curBlockLen;
                        ThreadPool.QueueUserWorkItem((offsetsTupleObj) => {
                            Tuple<Piece, int, int> offsetsTuple = (Tuple<Piece, int, int>)offsetsTupleObj;
                            try {
                                handler.RequestPiece(offsetsTuple.Item1, offsetsTuple.Item2, offsetsTuple.Item3, _outCancelToken);
                            } catch (OperationCanceledException) {

                            } finally {
                                _exchange.Realese(handler);
                            }
                        }, new Tuple<Piece, int, int>(nextPiece, start, curBlockLen));

                        start += curBlockLen;

                        // last block of piece, breaking
                        if (curBlockLen < _blockLen) {
                            break;
                        }


                    } while (start < nextPiece.PieceLength && !_outCancelToken.IsCancellationRequested);

                    stopped = _myCancelTokenOwner.IsCancellationRequested || _outCancelToken.IsCancellationRequested;

                } while (!stopped);

                if(stopped) {
                    break;
                }

                // Initial count
                pendingPiecesCountDown.Signal();
                Trace.WriteLine($"PiecePicker: Finished requesting, pending pieces count {pendingPiecesCountDown.CurrentCount}");
                if (pendingPiecesCountDown.Wait(WaitTimeoutForEndGamePiecesInMSec, _outCancelToken)) {
                    break;
                } else {
                    foreach (var uploader in _exchange.GetUploaders()) {
                        uploader.CancelPendingOutboundRequests();
                    }

                    pendingPiecesCountDown.Reset();
                }

            } while (!stopped);
            
            _exchange.Stop();
            Trace.WriteLine($"RequestManager: Finished requesting");
            Finished?.Invoke(); 
        }

        public void Stop() {
            _myCancelTokenOwner.Cancel();
        }
    }
}
