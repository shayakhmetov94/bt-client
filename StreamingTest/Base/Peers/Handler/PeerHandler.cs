using bittorrent_client.Base.Pieces;
using bittorrent_client.Base.Util;
using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net.Sockets;
using System.Threading;
using System.Threading.Tasks;

namespace bittorrent_client.Base.Peers.Handler
{
    public class PeerHandler : IEquatable<PeerHandler>, IDisposable
    {
        #region Internal structs 

        struct RequestInfo
        {
            public int pieceIdx;
            public int offset;
            public int length;
        }

        private enum MsgTypes
        {
            Choke = 0x00,
            Unchoking = 0x01,
            Interested = 0x02,
            NotInterested = 0x03,
            Have = 0x04,
            Bitfield = 0x05,
            Request = 0x06,
            Piece = 0x07,
            Cancel = 0x08,
            Port = 0x09,
            None = 0x10
        }

        #endregion

        #region Comparators

        public class DownloadRateComparator : IComparer<PeerHandler>
        {
            public int Compare(PeerHandler x, PeerHandler y) {
                if (x != null && y != null)
                    return x.DownloadRate.CompareTo(y.DownloadRate);
                return 0;
            }
        }

        public class UploadRateComparator : IComparer<PeerHandler>
        {
            public int Compare(PeerHandler x, PeerHandler y) {
                if (x != null && y != null)
                    return x.UploadRate.CompareTo(y.UploadRate);
                return 0;
            }
        }

        #endregion

        #region Props and constants

        /// <summary>
        /// TCP receive timeout
        /// </summary>
        private static readonly int TcpReceiveTimeoutInMsec = 0;

        /// <summary>
        /// TCP send timeout
        /// </summary>
        private static readonly int TcpSendTimeoutInMsec = 0;


        /// <summary>
        /// Max requests count for handler
        /// </summary>
        private static readonly int MaxRequestsCount = 20;

        /// <summary>
        /// Pending outbound requests queue
        /// </summary>
        private ConcurrentQueue<RequestInfo> _pendingOutboundRequests;

        /// <summary>
        /// Peer's input network socket stream
        /// </summary>
        private readonly Stream _input;

        /// <summary>
        /// Peer's listener task
        /// </summary>
        private readonly Task _listenerTask;

        /// <summary>
        /// True, if listener task was closed by exception
        /// </summary>
        private bool _isListenerStopped;

        /// <summary>
        /// Peer's id, not used yet
        /// </summary>
        private string _peerId;

        /// <summary>
        /// Semaphore to limit piece requests count for Peer
        /// </summary>
        private SemaphoreSlim _counterRqsSemaphore;

        /// <summary>
        /// Associated peer's client
        /// </summary>
        public BtClient Client { get; }

        /// <summary>
        /// Peer's v4 IP address
        /// </summary>
        public string PeerIp { get; }

        public delegate void UnchokeHandler(PeerHandler handler);
        public delegate void BitfieldHandler(PeerHandler handler);
        public delegate void InterestedHandler(PeerHandler handler);

        /// <summary>
        /// Invoked after receiving unchoke from peer
        /// </summary>
        public event UnchokeHandler OnUnchoke;

        /// <summary>
        /// Invoked after receiving interested from peer
        /// </summary>
        public event InterestedHandler OnInterested;

        /// <summary>
        /// Invoked after receiving bitfield from peer
        /// </summary>
        public event BitfieldHandler OnBitfield;

        /// <summary>
        /// Peer's TCP connection 
        /// </summary>
        public TcpClient Connection { get; }

        /// <summary>
        /// Pieces that peer have. Initially is null
        /// </summary>
        public BitField PiecesHave { get; }

        /// <summary>
        /// Peer's current state. Whether peer is interested, choking etc
        /// </summary>
        public Peer Peer { get; }

        /// <summary>
        /// Current download rate of peer
        /// </summary>
        public double DownloadRate { get; private set; }

        /// <summary>
        /// Current upload rate of peer
        /// </summary>
        public double UploadRate { get; private set; }

        #endregion

        /// <summary>
        /// Creates PeerHandler instance
        /// </summary>
        /// <param name="peer">Peer info</param>
        /// <param name="connection">TCP connection. Must be connected</param>
        /// <param name="client">Associated client</param>
        public PeerHandler(Peer peer, TcpClient connection, BtClient client) {
            _pendingOutboundRequests = new ConcurrentQueue<RequestInfo>();

            _input = new NetworkStream(connection.Client);
            _listenerTask = new Task(ListenIncoming);
            _isListenerStopped = false;

            _counterRqsSemaphore = new SemaphoreSlim(MaxRequestsCount);

            Client = client;
            PeerIp = peer.IpAddress.ToString();
            Connection = connection;
            Connection.ReceiveTimeout = TcpReceiveTimeoutInMsec;
            Connection.SendTimeout = TcpSendTimeoutInMsec;

            Peer = peer;
            PiecesHave = new BitField(Client.Metainfo.PiecesCount);
        }

        public void StartListening() {
            _listenerTask.Start();
        }

        public bool ReadAndValidateHandshake() {
            byte[] peerHs = ReadMsgSync(Client.Handshake.Length);

            if (peerHs[0] != Client.Metainfo.Protocol.Length) {
                return false;
            }

            for (var i = 1; i < peerHs[0]; i++) {
                if (peerHs[i] != Client.Metainfo.Protocol[i - 1]) {
                    return false;
                }
            }

            var clientInfoHs = Client.Metainfo.InfoHash;
            for (int i = peerHs[0] + 9, j = 0; j < 20; j++, i++) {//info hash
                if (peerHs[i] != clientInfoHs[j]) {
                    return false;
                }
            }

            byte[] tpeerId = new byte[20];
            Buffer.BlockCopy(peerHs, peerHs[0] + 29, tpeerId, 0, 20);
            _peerId = Client.DefaultEncoding.GetString(tpeerId);

            return true;
        }

        public void SendKeepAlive() {
            byte[] msg = new byte[4];
            try {
                _input.Write(msg, 0, msg.Length);
                _input.Flush();
            } catch (Exception e) {
                ProcessException(MsgTypes.None, e);
            }
        }

        public bool SendMyHandshake() {
            Debug.WriteLine($"PeerHandler {PeerIp}: Sending client handshake");
            try {
                _input.Write(Client.Handshake, 0, Client.Handshake.Length);
                _input.Flush();
            } catch (Exception) {
                return false;
            }

            return true;
        }

        public byte[] Handshake(byte[] clientHandshake) {
            byte[] handshkMsg = clientHandshake;
            byte[] responce = new byte[handshkMsg.Length];
            try {
                _input.Write(handshkMsg, 0, handshkMsg.Length);
                _input.Flush();
            } catch (Exception e) {
                ProcessException(MsgTypes.None, e);
            }

            var recv = _input.Read(responce, 0, responce.Length);
            return recv != 0 ? responce : null;
        }

        public void SendChoke(bool choke) {
            Debug.WriteLine($"{PeerIp}: Sending choke {choke}");
            byte[] msg = new byte[5];
            msg[3] = 1;
            msg[4] = (byte)(choke ? MsgTypes.Choke : MsgTypes.Unchoking);
            try {
                _input.Write(msg, 0, msg.Length);
                _input.Flush();
            } catch (Exception e) {
                ProcessException(MsgTypes.Choke, e);
            }

            Peer.IsChoked = choke;
        }

        public void SendInterested(bool interested) {
            Debug.WriteLine($"PeerHandler {PeerIp}: Sending interested {interested}");
            byte[] msg = new byte[5];
            msg[3] = 1;
            msg[4] = (byte)(interested ? MsgTypes.Interested : MsgTypes.NotInterested);
            try {
                _input.Write(msg, 0, msg.Length);
                _input.Flush();
                Peer.IsClientInterested = interested;
            } catch (Exception e) {
                ProcessException(MsgTypes.Interested, e);
            }
        }

        public void SendHave(int idx) {
            Debug.WriteLine($"PeerHandler {PeerIp}: Sending have, index = {idx}");
            var msg = new byte[10];
            msg[3] = 4;
            msg[4] = (byte)MsgTypes.Have;
            Buffer.BlockCopy(GetInt(idx), 0, msg, 6, 4);
            try {
                _input.Write(msg, 0, msg.Length);
                _input.Flush();
            } catch (Exception e) {
                ProcessException(MsgTypes.Have, e);
            }
        }

        public void SendBitfield(byte[] bits) {
            Debug.WriteLine($"PeerHandler {PeerIp}: Sending bitfield");
            int len = 5 + bits.Length;
            byte[] msg = new byte[len];
            Buffer.BlockCopy(GetInt(bits.Length + 1), 0, msg, 0, 4);
            msg[4] = (byte)MsgTypes.Bitfield;
            Buffer.BlockCopy(bits, 0, msg, 5, bits.Length);
            try {
                _input.Write(msg, 0, msg.Length);
                _input.Flush();
            } catch (Exception e) {
                ProcessException(MsgTypes.Bitfield, e);
            }
        }

        public void RequestPiece(Piece piece, int offset, int len, CancellationToken cancelToken) {
            Debug.WriteLine($"PeerHandler {PeerIp}: Requesting piece, Index = {piece.Index} Offset = {offset} Length = {len}");
            if (!_counterRqsSemaphore.Wait(1000, cancelToken)) {
                if (cancelToken.IsCancellationRequested) {
                    Trace.WriteLine($"PeerHandler {PeerIp}: request cancelled");
                    return;
                }

                Trace.WriteLine($"PeerHandler {PeerIp}: long waits for response...");
            }

            _pendingOutboundRequests.Enqueue(new RequestInfo()
            {
                pieceIdx = piece.Index,
                offset = offset,
                length = len
            });

            var msg = new byte[17];
            msg[3] = 13;
            msg[4] = (byte)MsgTypes.Request;
            Buffer.BlockCopy(GetInt(piece.Index), 0, msg, 5, 4);
            Buffer.BlockCopy(GetInt(offset), 0, msg, 9, 4);
            Buffer.BlockCopy(GetInt(len), 0, msg, 13, 4);
            try {
                _input.Write(msg, 0, msg.Length);
                _input.Flush();
            } catch (Exception e) {
                ProcessException(MsgTypes.Request, e);
            }
        }

        public void SendPiece(byte[] piece, int idx, int offset) {
            Debug.WriteLine($"PeerHandler {PeerIp}: Sending piece #{idx}");
            int msgLen = piece.Length + 13;
            byte[] msg = new byte[msgLen];
            Buffer.BlockCopy(GetInt(piece.Length + 9), 0, msg, 0, 4);
            msg[4] = (byte)MsgTypes.Piece;
            Buffer.BlockCopy(GetInt(idx), 0, msg, 5, 4);
            Buffer.BlockCopy(GetInt(offset), 0, msg, 9, 4);
            Buffer.BlockCopy(piece, 0, msg, 13, piece.Length);
            try {
                DateTime start = DateTime.Now;
                _input.Write(msg, 0, msg.Length);
                _input.Flush();
                UploadRate = msg.Length / (DateTime.Now - start).TotalMilliseconds;
            } catch (Exception e) {
                ProcessException(MsgTypes.Piece, e);
            }
        }

        public void Cancel(int idx, int offset, int len) {
            byte[] msg = new byte[17];
            msg[3] = 13;
            msg[4] = (byte)MsgTypes.Cancel;
            Buffer.BlockCopy(GetInt(idx), 0, msg, 5, 4);
            Buffer.BlockCopy(GetInt(offset), 0, msg, 9, 4);
            Buffer.BlockCopy(GetInt(len), 0, msg, 13, 4);
            try {
                _input.Write(msg, 0, msg.Length);
                _input.Flush();
            } catch (Exception e) {
                ProcessException(MsgTypes.Cancel, e);
            }

            if (_counterRqsSemaphore.CurrentCount > 0) {
                _counterRqsSemaphore.Release();
            }
        }

        public void CancelPendingOutboundRequests() {
            while (_pendingOutboundRequests.Count > 0) {
                if (_pendingOutboundRequests.TryDequeue(out RequestInfo rqInfo)) {
                    Cancel(rqInfo.pieceIdx, rqInfo.offset, rqInfo.length);
                }
            }
        }

        public void ProcessMessage(byte[] msg, int offset) {
            byte[] trunc = new byte[msg.Length - offset];
            Buffer.BlockCopy(msg, offset, trunc, 0, trunc.Length);
            ProcessMessage(trunc);
        }

        public void ProcessMessage(byte[] msg) {
            switch ((MsgTypes)msg[0]) {
                case MsgTypes.Choke:
                    Trace.WriteLine($"PeerHandler {PeerIp}: client choked");
                    Peer.IsChoking = true;
                    break;
                case MsgTypes.Unchoking:
                    Debug.WriteLine($"PeerHandler {PeerIp}: client unchoked");
                    Peer.IsChoking = false;
                    OnUnchoke?.Invoke(this);
                    break;
                case MsgTypes.Interested:
                    Debug.WriteLine($"PeerHandler {PeerIp}: interested");
                    Peer.IsInterested = true;
                    OnInterested?.Invoke(this);
                    break;
                case MsgTypes.NotInterested:
                    Debug.WriteLine($"PeerHandler {PeerIp}: not interested");
                    Peer.IsInterested = false;
                    break;
                case MsgTypes.Have:
                    var pieceIdx = ParseInt(msg, 1);
                    if (pieceIdx >= PiecesHave.Length) {
                        PiecesHave[0] = true;
                    } else {
                        PiecesHave.Set(pieceIdx, true);
                    }
                    break;
                case MsgTypes.Bitfield:
                    PiecesHave.FromArray(msg, 1, msg.Length);
                    OnBitfield?.Invoke(this);
                    break;

                case MsgTypes.Request:
                    ProcessRequest(msg);
                    break;
                case MsgTypes.Piece:
                    ProcessPiece(msg);
                    break;
                case MsgTypes.Cancel:
                    // TODO: create inbound requests queue
                    break;
                case MsgTypes.Port:
                    //int port = ParseInt(msg); //TODO: create full api with ext mainline
                    break;
            }
        }

        protected async void ListenIncoming() {
            while (!_isListenerStopped) {
                try {
                    int msgLen = ParseInt(await ReadMsg(4));

                    if (msgLen == 0) {
                        Debug.WriteLine($"No payload from {PeerIp}");
                        continue;
                    }

                    byte[] msg = await ReadMsg(msgLen);
                    if(msg == null) {//connection closed
                        break;
                    }

                    if (msg.Length == 0) {//keep-alive
                        Debug.WriteLine("Got keep-alive, peerId is " + _peerId);
                        continue;
                    }
                    ProcessMessage(msg);
                } catch (Exception e) {
                    Trace.WriteLine($"Listener task of PeerHandler {PeerIp} is dead, pending requests count = {_pendingOutboundRequests.Count} {e}");
                    if (e.InnerException != null) {
                        Debug.WriteLine($"Inner exception: {e.ToString()}");
                    }

                    ProcessException(MsgTypes.None, e);
                }
            }
        }

        private void ProcessException(MsgTypes msg, Exception e) {
            Debug.WriteLine("PeerHandler {0}: Got exception while processing message {2}: {1} ", PeerIp, e, msg);
            Disconnect();
        }

        private bool ValidateRq(int pieceIdx, int offset, int len) {
            if (Peer.IsChoked) {
                return false;
            }

            if (pieceIdx < 0 || pieceIdx > Client.Metainfo.PiecesCount) {
                return false;
            }

            if (offset < 0 || offset > Client.Metainfo.PieceLength) {
                return false;
            }

            if (len < 0 || len > offset + Client.Metainfo.PieceLength) {
                return false;
            }

            return true;
        }

        private void ProcessRequest(byte[] msg) {
            int pieceIdx = ParseInt(msg, 1),
                offset = ParseInt(msg, 5),
                len = ParseInt(msg, 9);
            Debug.WriteLine($"{PeerIp} handler: Processing request, idx = {pieceIdx}, offset = {offset}, len = {len}");
            if (ValidateRq(pieceIdx, offset, len)) {
                Piece requested = Client.PieceStorage.GetPiece(pieceIdx);
                if (requested.IsValid) {
                    byte[] reqPayload = new byte[len];
                    Buffer.BlockCopy(requested.Payload, offset, reqPayload, 0, len);
                    SendPiece(reqPayload, pieceIdx, offset);
                }
            }
        }

        private void ProcessPiece(byte[] msg) {
            int pieceIdx = ParseInt(msg, 1);
            int offset = ParseInt(msg, 5);

            DateTime start = DateTime.Now;
            DownloadRate = msg.Length / (DateTime.Now - start).TotalSeconds;
            Piece piece = Client.PieceStorage.GetPiece(pieceIdx);

            for (int i = 0; i < _pendingOutboundRequests.Count; i++) {
                if (_pendingOutboundRequests.TryDequeue(out RequestInfo rqInfo)) {
                    //usually would break in first iteration
                    if (rqInfo.pieceIdx == pieceIdx &&
                     rqInfo.offset == offset &&
                     rqInfo.length == msg.Length - 9
                    ) {
                        break;
                    } else {
                        //but if not - would mess up entire queue, queue still eventually will return to order
                        _pendingOutboundRequests.Enqueue(rqInfo);
                    }
                }
            }

            piece.Write(this, msg, offset, 9);
            _counterRqsSemaphore.Release();
        }

        protected byte[] GetInt(int num) {
            var raw = BitConverter.GetBytes(num);
            Array.Reverse(raw);
            return raw;
        }

        protected int ParseInt(byte[] msg, int startPos = 0) {
            Array.Reverse(msg, startPos, 4);
            return (int)BitConverter.ToUInt32(msg, startPos);
        }

        public async Task<byte[]> ReadMsg(int msgLen) {
            byte[] msg = new byte[msgLen];
            int bufLen = Client.Metainfo.PieceLength / 4;
            byte[] buf = new byte[bufLen];
            try {
                int offset = 0;
                while (offset < msgLen) {
                    int readCnt = msgLen - offset;
                    int recv = await _input.ReadAsync(buf, 0, readCnt > bufLen ? bufLen : readCnt);
                    Buffer.BlockCopy(buf, 0, msg, offset, recv);
                    offset += recv;
                }
            } catch(SocketException se) {
                Debug.WriteLine($"{PeerIp} handler: connection closed {se}");
                return null;
            }

            return msg;
        }

        public byte[] ReadMsgSync(int msgLen) {
            byte[] msg = new byte[msgLen];
            int bufLen = Client.Metainfo.PieceLength / 4;
            byte[] buf = new byte[bufLen];

            int offset = 0;
            try {
                while (offset < msgLen) {
                    int readCnt = msgLen - offset;
                    int recv = _input.Read(buf, 0, readCnt > bufLen ? bufLen : readCnt);
                    Buffer.BlockCopy(buf, 0, msg, offset, recv);
                    offset += recv;
                }
            } catch (Exception e) {
                ProcessException(MsgTypes.None, e);
            }

            return msg;
        }


        public void Disconnect() {
            Debug.WriteLine($"PeerHandler {PeerIp}: Shutdown requested");
            CancelPendingOutboundRequests();
            _isListenerStopped = true;
            _input.Close();
            Connection.Close();
        }

        public override int GetHashCode() {
            return PeerIp.GetHashCode();
        }

        public override bool Equals(object obj) {
            return obj is PeerHandler handler && Equals(handler);
        }

        public bool Equals(PeerHandler other) {
            return other?.PeerIp == PeerIp;
        }

        public void Dispose() {
            Debug.WriteLine($"Handler {PeerIp} is disposed");
            _listenerTask.Dispose();
            _counterRqsSemaphore.Dispose();
            Disconnect();
        }
    }
}