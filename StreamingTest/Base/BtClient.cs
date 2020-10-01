using bittorrent_client.Base.Announce;
using bittorrent_client.Base.Dht;
using bittorrent_client.Base.Peers;
using bittorrent_client.Base.Peers.Handler;
using bittorrent_client.Base.Pieces;
using bittorrent_client.Base.Storage;
using bittorrent_client.Base.Strategies;
using bittorrent_client.Base.Util;
using bittorrent_client.Pool;
using System;
using System.IO;
using System.Net;
using System.Text;
using System.Threading;

namespace bittorrent_client.Base
{   //TODO: test upload
    public class BtClient : IBtClient
    {
        /// <summary>
        /// Client's endpoint 
        /// </summary>
        private IPEndPoint _endPoint;

        /// <summary>
        /// Client's identifier, https://wiki.theory.org/index.php/BitTorrentSpecification#peer_id
        /// </summary>
        private byte[] _peerId { get; }

        #region IBtClient
        public BtClientStats GetStats()
        {
            return new BtClientStats {
                Downloaded      = this.Downloaded,
                Left            = this.Left,
                Uploaded        = this.Uploaded
            };
        }

        public IPEndPoint GetEndPoint() {
           return this._endPoint;
        }

        public byte[] GetInfoHash()
        {
            return Metainfo.InfoHash;
        }

        public byte[] GetPeerID()
        {
            return _peerId;
        }

        public Piece GetPiece(int idx) {
            return PieceStorage.GetPiece(idx);
        }
        #endregion

        /// <summary>
        ///  Used for encoding outgoing and decoding incoming messages
        /// </summary>
        public Encoding DefaultEncoding { get; private set; }

        /// <summary>
        /// First five bytes of PeerId
        /// </summary>
        public string ClientId { get; } = "GNRCK";

        /// <summary>
        /// Client's handshake message
        /// </summary>
        public byte[] Handshake { get; private set; }

        /// <summary>
        /// Piece block length used by Client
        /// </summary>
        public int BlockLength { get; private set; }

        /// <summary>
        /// Blocks count in piece 
        /// </summary>
        public int BlocksInPieceCount { get; private set; }

        /// <summary>
        /// AnnounceManager. Periodically queries HTTP trackers
        /// </summary>
        public AnnounceManager AnnounceManager { get; private set; }

        /// <summary>
        /// MainlineManager. Mainline DHT client
        /// </summary>
        public MainlineManagerExt MainlineManager { get; private set; }

        /// <summary>
        /// ConnectionHub. Initiates and closes connections between peers 
        /// </summary>
        public ConnectionHub ConnectionManager { get; private set; }

        /// <summary>
        /// HandlerExchange. Active peers exchange. Used for acquiring and evaluating peers 
        /// </summary>
        public HandlerExchange HandlerExchange { get; private set; }

        /// <summary>
        /// PieceStorage. Storage for caching pieces
        /// </summary>
        public IPieceStorage PieceStorage { get; private set; }

        /// <summary>
        /// RequestManager. Requests piece blocks from peers
        /// </summary>
        public PiecePicker PiecePicker { get; private set; }

        /// <summary>
        /// PeerStateCache. 
        /// </summary>
        public PeerStateCache PeerStateCache { get; private set; }

        #region Counters
        public int Downloaded { get; private set; }
        public int Left { get; private set; }
        public int Uploaded { get; private set; }
        #endregion

        /// <summary>
        /// Torrent meta info
        /// </summary>
        public Metainfo Metainfo { get; private set; }

        /// <summary>
        /// Creates new BtClient
        /// </summary>
        /// <param name="meta">Torrent metainfo</param>
        /// <param name="clientEndPoint">Client's external network endpoint. Must be accessible by other peers</param>
        /// <param name="defaultEncoding">Message text encoding</param>
        /// <param name="output">Output stream. If torrent contains multiple files </param>
        /// <param name="blockLen">Piece block length in bytes. 10^14 bytes if not specified</param>
        /// <param name="fileName">File name to download. If not specified all files will be downloaded</param>
        /// <param name="dhtEndPoint">Mainline DHT's client endpoint. DHT will not be used if parameter is null</param>
        /// <param name="storage">Storage for caching pieces. If not specified MemCachedPieceStorage will be used</param>
        /// <param name="cancelToken">Requesting cancel on this CancellationToken causes forcefull shutdown of client</param>
        public BtClient(Metainfo meta, IPEndPoint clientEndPoint, Encoding defaultEncoding, Stream output, int blockLen = 16384, string fileName = null, PiecePickStrategy piecePickStrategy = PiecePickStrategy.RarestFirst, IPEndPoint dhtEndPoint = null, IPieceStorage storage = null, CancellationToken? cancelToken = null) {
            Metainfo            = meta;
            _endPoint            = clientEndPoint;
            DefaultEncoding     = defaultEncoding;
            _peerId              = GeneratePeerId();
            Handshake           = CalcHandshake();
            BlockLength         = blockLen;
            BlocksInPieceCount  = Metainfo.PieceLength / BlockLength;

            AnnounceManager     = new AnnounceManager(this, Metainfo.Announces);
            IResourcePool<Peers.Peer> peersPool = AnnounceManager;
            if(dhtEndPoint != null) {
                MainlineManager = new MainlineManagerExt(clientEndPoint, dhtEndPoint, meta.InfoHash);

                peersPool = new MergedPool<Peer>(new IResourcePool<Peer>[] {
                    AnnounceManager,
                    MainlineManager
                });
            }

            if (storage == null) {
                storage = new FileStorage(Metainfo, BlockLength);
            }

            PieceStorage = storage;
            BitField piecesHave = PieceStorage.GetValidPieces();

            IRequestStrategy rqStrat = null;
            switch (piecePickStrategy) {
                case PiecePickStrategy.RarestFirst:
                    RarestFirstRqStrategy rarestFirstRqStrategy = new RarestFirstRqStrategy(Metainfo.PiecesCount, piecesHave);
                    rqStrat = rarestFirstRqStrategy;
                    PeerStateCache = new PeerStateCache(rarestFirstRqStrategy);
                    break;
                case PiecePickStrategy.Random:
                    rqStrat = new RandomPieceRqStrategy(Metainfo.PiecesCount, piecesHave);
                    break;
                case PiecePickStrategy.Sequential:
                    rqStrat = new SequentialPieceRqStrategy(Metainfo.PiecesCount);
                    break;
                case PiecePickStrategy.SequentialOneFile:
                    if (string.IsNullOrEmpty(fileName)) {
                        throw new ArgumentException("fileName");
                    }

                    rqStrat = new SequentialPieceRqStrategyOneFile(Metainfo, fileName);
                    break;
                default:
                    break;
            }

            if (PeerStateCache == null) {
                PeerStateCache = new PeerStateCache();
            }

            ConnectionManager = new ConnectionHub(this, clientEndPoint, peersPool, PeerStateCache);

            HandlerExchange = new HandlerExchange(ConnectionManager, PeerStateCache);

            PiecePicker = new PiecePicker(this, HandlerExchange, rqStrat, BlockLength, cancelToken);
        }

        private byte[] GeneratePeerId() {
            MemoryStream ms = new MemoryStream(20);
            byte[] clientIdBytes = DefaultEncoding.GetBytes(ClientId);
            ms.Write(clientIdBytes, 0, clientIdBytes.Length);
            byte[] dateBytes = BitConverter.GetBytes(DateTime.Now.ToBinary().GetHashCode());
            byte hash = 0;
            foreach (byte b in dateBytes) {
                hash ^= b;
            }

            ms.WriteByte(hash);
            ms.WriteByte((byte)System.Diagnostics.Process.GetCurrentProcess().Id);
            int padCount = (int)(20 - ms.Length);
            for(int i = padCount; i > 0; i--) {
                ms.WriteByte((byte)'0');
            }

            return ms.ToArray();
        }

        private byte[] CalcHandshake() {
            byte[] msg = new byte[Metainfo.Protocol.Length + 49];
            byte protoLen = (byte)Metainfo.Protocol.Length;
            msg[0] = protoLen;
            System.Buffer.BlockCopy(Metainfo.Protocol, 0, msg, 1, protoLen);
            int infoHashLen = Metainfo.InfoHash.Length;
            int offset = protoLen + 9;//8 bytes are reserved
            if(MainlineManager != null) {
                msg[offset - 1] = 0x01;//DHT supported
            }
            System.Buffer.BlockCopy(Metainfo.InfoHash, 0, msg, offset, infoHashLen);
            offset += infoHashLen;
            System.Buffer.BlockCopy(_peerId, 0, msg, offset, _peerId.Length);
            return msg;
        }

        /// <summary>
        /// Start requesting pieces
        /// </summary>
        public void Start(bool stopAfterFinish = true) {
            AnnounceManager.Start();
            if (stopAfterFinish) {
                PiecePicker.Finished += Stop;
            }
            PiecePicker.Start();
        }

        /// <summary>
        /// Gracefully shutdown the client
        /// </summary>
        public void Stop() {
            AnnounceManager.Stop();
            PiecePicker.Stop();
            PieceStorage.Dispose();
        }
    }
}
