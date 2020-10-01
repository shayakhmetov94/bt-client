using bittorrent_client.Base.Peers.Handler;
using bittorrent_client.Base.Util;
using System;
using System.Diagnostics;
using System.Security.Cryptography;

namespace bittorrent_client.Base.Pieces
{
    public class Piece
    {
        public delegate void BlockDone(PeerHandler from, Piece piece, int offset, int len);

        public delegate void PieceDone(Piece piece);

        private static readonly SHA1 Sha1Algo = SHA1.Create();

        private readonly BitField _blocks;
        private readonly int _blockLen;
        private readonly object _wirteLock = new object();

        public Piece(int idx, int pieceLen, int blockLen, byte[] payload = null, byte[] blocks = null, bool isValid = false) {
            Index = idx;
            PieceLength = pieceLen;
            Payload = payload ?? new byte[PieceLength];
            _blocks = blocks == null ? new BitField((int)Math.Ceiling((double)PieceLength / blockLen)) : new BitField(blocks, 0, blocks.Length);
            _blockLen = blockLen;
            IsValid = isValid;
        }
        
        public int Index { get; }

        public byte[] Payload { get; }

        public int PieceLength { get; }

        public BitField Blocks { get { return _blocks.Clone(); } }
        
        public bool IsValid { get; set; }

        public event BlockDone OnBlockDownload;
        public event PieceDone OnPieceDone;

        public bool Write(PeerHandler from, byte[] msg, int pieceOffset, int msgOffset) {
            Debug.WriteLine("Writing block to piece #{0}. Piece offset = {1}, Msg offset = {2}", Index, pieceOffset,
                msgOffset);

            var blockIdx = pieceOffset / _blockLen;

            if (_blocks[blockIdx]) {
                Debug.WriteLine("Piece #{0} already contains block with offset {1}", Index, pieceOffset);
                return false;
            }

            lock (_wirteLock) {
                _blocks.Set(blockIdx, true);
            }

            Buffer.BlockCopy(msg, msgOffset, Payload, pieceOffset, msg.Length - msgOffset);
            OnBlockDownload?.Invoke(from, this, pieceOffset, _blockLen);
            if ( _blocks.AllTrue) {
                Debug.WriteLine("Blocks of piece #{0} are downloaded, validating...", Index);
                IsValid = true; //TODO: _client.ValidatePiece(this);
                if (IsValid) { 
                    Debug.WriteLine("Piece #{0} is valid ", Index);
                }
                else {
                    _blocks.SetAll(false);
                    Debug.WriteLine("Piece #{0} is not valid ", Index);
                }
                OnPieceDone?.Invoke(this);
            }
            return true;
        }

        public override string ToString() {
            return $"Piece #{Index}";
        }
    }
}