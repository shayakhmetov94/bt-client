using bittorrent_client.Base.Peers;
using bittorrent_client.Base.Strategies;
using commons_bittorrent.Base.Bencoding;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Threading;

namespace bittorrent_client.Base.Dht
{
    public class MainlineManagerExt : IResourcePool<Peer> {
        private static readonly string GETPEERS_KEY = "GETPEERS";

        private object __peersSearchLck = new object();
        private CancellationTokenSource _myCancelTokenSrc;

        private TcpClient _extMnlClient;
        private NetworkStream _extMnlClientStream;
        private readonly byte[] _buff;
        private IPEndPoint _extMnlEp;
        private BlockingCollection<Peers.Peer> _myPool;
        private byte[] _infoHash;
        

        public MainlineManagerExt(IPEndPoint clientEndPoint, IPEndPoint mnlDhtEndPoint, byte[] fixedInfoHash) {
            _extMnlClient = new TcpClient(mnlDhtEndPoint);
            _extMnlEp = mnlDhtEndPoint;
            _myPool = new BlockingCollection<Peers.Peer>();
            _myCancelTokenSrc = new CancellationTokenSource();
            _infoHash = fixedInfoHash;
            _buff = new byte[1024];
            try {
                _extMnlClient.Connect(mnlDhtEndPoint);
            } catch(SocketException se) {
                Debug.WriteLine($"Can't initialize connection to remote DHT: {se}");
                return;
            }

            _extMnlClientStream = _extMnlClient.GetStream();
            ListenResponses();
        }

        public Peer Acquire() {
            if(_myPool.Count > 0) {
                return _myPool.Take();
            }

            InvokeGetPeers(_infoHash, false, false);

            return _myPool.Take();
        }

        public Peer Acquire(CancellationToken token) {
            if(_myPool.Count > 0) {
                return _myPool.Take();
            }

            InvokeGetPeers(_infoHash, false, false);

            return _myPool.Take(token);
        }

        public void Dispose(Peers.Peer resource) {
            
        }

        public void Realese(Peers.Peer resource) {
            
        }

        private void InvokeGetPeers(byte[] infohash, bool fast, bool nocache) {
            lock(__peersSearchLck) {
                Dictionary<string, Object> argsDict = new Dictionary<string, object>();
                List<Object> args = new List<Object> { GETPEERS_KEY, new System.Runtime.Remoting.Metadata.W3cXsd2001.SoapHexBinary(infohash).ToString() };

                if(fast) {
                    args.Add("-fast");
                }
                if(nocache) {
                    args.Add("-nocache");
                }

                argsDict["arguments"] = args;
                argsDict["cwd"] = ".";

                Bencoder beEncoder = new Bencoder();
                byte[] bencodedArgs = beEncoder.EncodeElement(argsDict);
                while(!_myCancelTokenSrc.IsCancellationRequested) {
                    try {
                        _extMnlClientStream.Write(GetInt(bencodedArgs.Length), 0, 4);
                        _extMnlClientStream.Write(bencodedArgs, 0, bencodedArgs.Length);
                        _extMnlClientStream.Flush();
                    } catch(SocketException se) {
                        Debug.WriteLine($"Can't connect to remote table {se}, waiting 10 secs and trying once more");
                        Thread.Sleep(10000);
                        continue;
                    }

                    break;
                }
            }
        }

        private void ListenResponses() {
           new Thread(() => {
                Bencoder bencoder = new Bencoder();
                while(!_myCancelTokenSrc.IsCancellationRequested) {
                   int len = ParseInt(ReadMsgSync(4));
                    byte[] incMsg = ReadMsgSync(len);

                    var decoded = (Dictionary<string, object>)bencoder.DecodeElement(incMsg);
                    string action = (string)decoded["action"];
                    switch(action) {
                    case "sysout":
                       _myPool.Add(new Peer(IPAddress.Parse((string)decoded["ip"]), int.Parse((string)decoded["port"])));
                       break;
                    }

                }
            }).Start();
        }

        public byte[] ReadMsgSync(int msgLen) {
            var msg = new byte[msgLen];
            var offset = 0;

            while(offset < msgLen) {
                var readCnt = msgLen - offset;
                var recv = _extMnlClientStream.Read(_buff, 0, readCnt > _buff.Length? _buff.Length : readCnt);
                Buffer.BlockCopy(_buff, 0, msg, offset, recv);
                offset += recv;
            }

            return msg;
        }

        protected int ParseInt(byte[] msg, int startPos = 0) {
           Array.Reverse(msg, startPos, 4);
            return (int)BitConverter.ToUInt32(msg, startPos);
        }

        protected byte[] GetInt(int num) {
            var raw = BitConverter.GetBytes(num);
            Array.Reverse(raw);
            return raw;
        }
    }
}
