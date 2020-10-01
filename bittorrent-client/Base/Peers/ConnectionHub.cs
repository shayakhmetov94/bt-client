using bittorrent_client.Base.Peers.Handler;
using bittorrent_client.Base.Strategies;
using bittorrent_client.Base.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace bittorrent_client.Base.Peers
{
    public class ConnectionHub : IResourcePool<PeerHandler>
    {
        private static readonly int TCPConnectAwaitTimeoutInMsecs = 1000;

        private BtClient _client;
        private IResourcePool<Peer> _peerPool;
        private PeerStateCache _stateCache;
        private Task _listenerTask, _fetchFromPeerPoolTask;
        private TcpListener _listener;
        private BlockingCollection<PeerHandler> _myPool;
        private SortedSet<string> _connectedIps;
        private bool _isListenerStopped;

        private CancellationTokenSource _myCancelTokenSource;

        private HandlerExchange _handlerExchange;

        public IPEndPoint EndPoint { get { return ((IPEndPoint)_listener.LocalEndpoint); } }

        public ConnectionHub(BtClient client, IPEndPoint endPoint, IResourcePool<Peer> peerPool, PeerStateCache stateCache) {
            _client = client;
            _peerPool = peerPool;
            _stateCache = stateCache;

            _myPool = new BlockingCollection<PeerHandler>();
            _listenerTask = new Task(StartListener);
            _listener = new TcpListener(endPoint);

            _connectedIps = new SortedSet<string>();
            _isListenerStopped = false;
            _myCancelTokenSource = new CancellationTokenSource();
        }

        public PeerHandler Acquire() {
            if (_myPool.Count > 0) {
                return _myPool.Take();
            }

            TryTakeFromPeerPool();

            return _myPool.Take();
        }

        public PeerHandler Acquire(CancellationToken token) {
            TryTakeFromPeerPool(token);
            try {
                var handler = _myPool.Take(token);
                if (handler == null) {
                    Debug.WriteLine($"ConnectionHub: breaking lock");
                }

                return handler;
            } catch (OperationCanceledException) {
                return null;
            }
        }

        private void TryTakeFromPeerPool(CancellationToken? token = null) {
            CancellationToken cancelToken = token == null ? _myCancelTokenSource.Token : token.Value;
            if (_fetchFromPeerPoolTask == null || _fetchFromPeerPoolTask.Status != TaskStatus.Running) {
                _fetchFromPeerPoolTask = Task.Factory.StartNew(TakeFromPeerPoolObj, cancelToken);
            }
        }

        private void TakeFromPeerPoolObj(object cancelTokenObj) {
            TakeFromPeerPool((CancellationToken)cancelTokenObj);
        }

        private void TakeFromPeerPool(CancellationToken token) {
            PeerHandler handler = null;

            while (handler == null) {
                var peer = _peerPool.Acquire(token);
                if (peer == null) {
                    _myPool.Add(null);
                    break;
                }

                if (!_connectedIps.Contains(peer.IpAddress.ToString())) {
                    handler = ConnectTo(peer);
                    if (handler != null) {
                        handler.SendBitfield(_client.PieceStorage.GetValidPieces().ToByteArray());
                        AwaitBitfield(handler);
                        handler.StartListening();
                        return;
                    } else {
                        _peerPool.Realese(peer);
                    }
                }
            }

        }

        public void Realese(PeerHandler handler) {
            try {
                if (handler.Connection.Connected) {
                    _myPool.Add(handler);
                    return;
                } else {
                    _connectedIps.Remove(handler.Peer.IpAddress.ToString());
                    _peerPool.Realese(handler.Peer);
                }
            } catch { }

            _peerPool.Dispose(handler.Peer);
        }

        public void StartAcceptingPeers(HandlerExchange handlerExchange) {
            if (_handlerExchange == null) {
                throw new ArgumentNullException("handlerExchange");
            }

            _handlerExchange = handlerExchange;

            try {
                _listener.Start();
                _listenerTask.Start();
            } catch (Exception e) {
                Debug.WriteLine($"ConnectionHub: Listener task is dead, exception {e}");
            }
        }

        private void StartListener() {
            while (!_isListenerStopped) {
                try {
                    TcpClient incPeer = _listener.AcceptTcpClient();
                    if (incPeer != null) {
                        ThreadPool.QueueUserWorkItem(AcceptIncomingObj, incPeer);
                    }
                } catch (SocketException se) {
                    Debug.WriteLine($"ConnectionHub: Exception while accepting client {se}");
                }
            }
        }

        private void AcceptIncomingObj(object conObj) {
            var connection = (TcpClient)conObj;
            var handler = AcceptIncoming(connection);

            if (handler != null) {
                _myPool.Add(handler);
                _connectedIps.Add(handler.Peer.IpAddress.ToString());
            }
        }

        private PeerHandler AcceptIncoming(TcpClient connection) {
            IPEndPoint ipAddress = (IPEndPoint)connection.Client.LocalEndPoint;
            PeerHandler handler = new PeerHandler(new Peer(ipAddress.Address, ipAddress.Port), connection, _client);

            if (handler.ReadAndValidateHandshake()) {
                if (handler.SendMyHandshake()) {
                    Debug.WriteLine($"Got new incoming peer {ipAddress}");
                    handler.SendBitfield(_client.PieceStorage.GetValidPieces().ToByteArray());
                    handler.StartListening();
                    return handler;
                } else {
                    Debug.WriteLine($"Peer {ipAddress} did not accept our handshake");
                }
            } else {
                Debug.WriteLine($"Can't validate handshake of new peer {ipAddress}");
            }

            return null;
        }

        public PeerHandler ConnectTo(Peer peer) {
            Debug.WriteLine($"ConnectionHub: Attempting to connect to {peer.IpAddress}");

            TcpClient peerCon = new TcpClient();
            try {
                peerCon.ConnectAsync(peer.IpAddress, peer.Port).Wait(TCPConnectAwaitTimeoutInMsecs);
                if (peerCon.Connected) {
                    PeerHandler handler = new PeerHandler(peer, peerCon, _client);
                    if (handler.SendMyHandshake()) {
                        if (handler.ReadAndValidateHandshake()) {
                            if (_handlerExchange != null) {
                                handler.OnInterested += (h) => { _handlerExchange.AddDownloader(h); };
                            }

                            return handler;
                        }
                        Debug.WriteLine($"Can't validate handshake of {peer.IpAddress}");
                    } else
                        Debug.WriteLine($"Peer {peer.IpAddress} did not accept our handshake");
                }
            } catch (SocketException se) {
                Trace.WriteLine($"ConnectionHub: can't connect to {peer.IpAddress}. Exception {se}");
            } catch (AggregateException ae) {
                Debug.WriteLine($"ConnectionHub: can't connect to {peer.IpAddress}. Exception {ae.InnerException}");
            }

            Debug.WriteLine($"ConnectionHub: connect attempt to {peer.IpAddress} failed");
            return null;
        }

        public void AwaitBitfield(PeerHandler handler) {
            _stateCache.AwaitBitfield(handler, this, 30);
        }

        public void Stop() {
            Debug.WriteLine($"ConnectionHub: Stop is requested");
            _isListenerStopped = true;
            try {
                _listener.Stop();
            } catch (SocketException se) {
                Debug.WriteLine($"ConnectionHub: Can't close listener properly, exception {se}");
            }

            while (_myPool.Count > 0) {
                Dispose(_myPool.Take());
            }
        }

        public void Dispose(PeerHandler handler) {
            handler.Disconnect();
            _connectedIps.Remove(handler.PeerIp);
            _peerPool.Dispose(handler.Peer);
        }
    }
}
