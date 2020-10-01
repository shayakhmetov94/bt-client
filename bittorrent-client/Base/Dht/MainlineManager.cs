using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Net;

using mainline_dht.Base;
using StreamingTest.Pool;
using Bittorrent;
using System.Collections.Concurrent;
using StreamingTest.Bittorrent.Base;
using System.Threading;
using FluentScheduler;
using System.Diagnostics;

namespace StreamingTest.Dht
{
    class MainlineManager : IResourcePool<Peer> {
        private object __searchLock;

        private BtClient _client;
        private SortedSet<string> _peersHave;
        private BlockingCollection<Peer> _peers;
        private CancellationTokenSource _myCancellationTokenSource;

        public HashTable HashTable { get; private set; }

        public MainlineManager(BtClient client, IPEndPoint dhtEndPoint, IEnumerable<IPEndPoint> knownNodeEndPoints) {
            __searchLock = new object();
            _client = client;
            HashTable = new HashTable(new Node(dhtEndPoint), knownNodeEndPoints, client.EndPoint.Port, null, true);
            _peers = new BlockingCollection<Peer>();
            _peersHave = new SortedSet<string>();

            _myCancellationTokenSource = new CancellationTokenSource();
        }

        public Peer Acquire() {
            if(_peers.Count > 0) {
                return _peers.Take();
            }

            TryInvokeInfohashQuery();
            
            var peer = _peers.Take();
            Debug.WriteLine($"Taking peer: {peer.IpAddress}");
            return peer;
        }

        public void Dispose(Peer resource) {
            _peersHave.Remove(resource.IpAddress.ToString());
        }

        public void Realese(Peer resource) {
            _peers.Add(resource);
        }

        public Peer Acquire(CancellationToken token) {
            TryInvokeInfohashQuery(token);

            var peer = _peers.Take(token);
            if(peer == null) {
                Debug.WriteLine($"MainlineManager: breaking lock");
            }
            
            return peer;
        }

        private void TryInvokeInfohashQuery(CancellationToken? cancelToken = null) {
            CancellationToken cancellationToken = cancellationToken == null ? _myCancellationTokenSource.Token : cancelToken.Value;
            Debug.WriteLine($"Trying to start info hash query");
            
            TryInvokeInfohashQueryObj(cancellationToken);
        }

        private void TryInvokeInfohashQueryObj(object cancelTokenObj) {
            QueryHashTable((CancellationToken)cancelTokenObj);
        }

        private void QueryHashTable(CancellationToken cancelToken) {
            lock(__searchLock) {
                Debug.WriteLine($"Starting info hash query");
                var peersPool = HashTable.FindPeers(new Id(_client.Metainfo.InfoHash), cancelToken);
                try {
                    while(true) {
                        ContactNode peer = peersPool.Take(cancelToken);
                        if(peer == null) {
                            _peers.Add(null);
                            break;
                        }

                        IPAddress contactAdddres = peer.EndPoint.Address;
                        if(!_peersHave.Contains(peer.EndPoint.Address.ToString())) {
                            _peers.Add(new Peer(peer.EndPoint.Address, peer.UtpPort));
                            _peersHave.Add(peer.EndPoint.Address.ToString());
                        }

                    }
                } catch(OperationCanceledException) { }
            }
        }
    }
}
