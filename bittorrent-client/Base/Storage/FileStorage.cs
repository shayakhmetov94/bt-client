using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using bittorrent_client.Base.Pieces;
using bittorrent_client.Base.Util;

namespace bittorrent_client.Base.Storage
{
    class FileStorage : IPieceStorage
    {
        #region Internal structs

        struct FileStreamInfo {
            public Metainfo.File File { get; set; }
            public FileStream Stream { get; set; } 
        }

        #endregion

        private readonly int CacheSize = 32;

        private SHA1Managed sha1 = new SHA1Managed();
        private MemCachedPieceStorage _cache;
        private Metainfo _metainfo;
        private List<FileStreamInfo> _fileStreams;
        private byte[] _piecesHash;
        private int _pieceLen;
        private int _blockLen;
        private BitField _piecesHave;

        public FileStorage(Metainfo metaInfo, int blockLength) {
            if(metaInfo == null) {
                throw new ArgumentNullException("metaInfo");
            }

            _cache = new MemCachedPieceStorage(CacheSize, blockLength, metaInfo.PieceLength);
            _metainfo = metaInfo;
            _fileStreams = new List<FileStreamInfo>(metaInfo.Files.Count);
            foreach (var file in metaInfo.Files) {
                string filePath = file.Path != null ? file.Path[0] : file.Name;
                FileStream fs = new FileStream(filePath, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.ReadWrite);
                fs.SetLength(file.Length);
                _fileStreams.Add(new FileStreamInfo()
                {
                    Stream = fs,
                    File = file
                });
            }
            _piecesHash = _metainfo.PiecesHash;
            _pieceLen = _metainfo.PieceLength;
            _blockLen = blockLength;

            _piecesHave = GetValidPieces();
        }

        public BitField GetValidPieces() {
            BitField validPieces = new BitField(_metainfo.PiecesCount);
            int readCount = 0,
                offset = 0,
                piece = 0;

            byte[] buf = new byte[_pieceLen];
            byte[] piecesHash = _metainfo.PiecesHash;

            foreach (FileStreamInfo fileStreamInfo in _fileStreams) {
                FileStream fs = fileStreamInfo.Stream;
                int fileLength = fileStreamInfo.File.Length;
                while (offset < fileLength) {
                    int totalRead = 0;
                    do {
                        readCount = fs.Read(buf, totalRead, _pieceLen);
                        totalRead += readCount;
                    } while (totalRead < _pieceLen);

                    validPieces[piece] = isValid(piece, buf);

                    offset += readCount;
                    piece++;
                }

                offset = 0;
                fs.Seek(0, SeekOrigin.Begin);
            }

            return validPieces;
        }

        public bool Contains(int idx) {
            return _cache.Contains(idx);
        }

        public int Count() {
            return _cache.Count();
        }

        public Piece CreateNew(int idx) {
            Piece newPiece = _cache.CreateNew(idx);
            newPiece.OnPieceDone += WritePiece;

            return newPiece;
        }

        public Piece GetPiece(int idx) {
            if(_cache.Contains(idx)) {
                return _cache.GetPiece(idx);
            }

            if (_piecesHave[idx]) {
                byte[] payload = ReadPiece(idx);
                Piece piece = new Piece(idx, _pieceLen, _blockLen, payload, isValid: isValid(idx, payload));
                _cache.Put(piece);
                return piece;
            }

            _piecesHave[idx] = true;
            return CreateNew(idx);
        }

        public bool Remove(int idx) {
            throw new NotSupportedException();
        }

        private byte[] ReadPiece(int idx) {
            FileStreamInfo fileStreamInfo = Seek(idx);
            int pieceLength = fileStreamInfo.File.Length / _pieceLen == idx ? (idx * _pieceLen) - fileStreamInfo.File.Length : _pieceLen;
            byte[] payload = new byte[pieceLength];
            fileStreamInfo.Stream.Read(payload, 0, pieceLength);

            return payload;
        }

        private void WritePiece(Piece p) {
            FileStream fs = Seek(p.Index).Stream;
            fs.Write(p.Payload, 0, p.Payload.Length);
            fs.Flush();
            p.OnPieceDone -= WritePiece;
        }

        private bool isValid(int idx, byte[] payload) {
            byte[] fileHash = sha1.ComputeHash(payload, 0, payload.Length);
            int piecesHashOffset = idx * 20;
            for (int i = 0, j = piecesHashOffset; i < fileHash.Length && j < piecesHashOffset + fileHash.Length; i++, j++) {
                if(fileHash[i] != _piecesHash[j]) {
                    return false;
                }
            }

            return true;
        }

        private FileStreamInfo Seek(int idx) {
            foreach(FileStreamInfo fileStreamInfo in _fileStreams) {
                if (Math.Ceiling((double)fileStreamInfo.File.Length / _metainfo.PieceLength) >= idx) {
                    FileStream fs = fileStreamInfo.Stream;
                    fs.Seek(idx * _pieceLen, SeekOrigin.Begin);
                    return fileStreamInfo;
                }
            }

            throw new ArgumentOutOfRangeException($"idx: {idx}");
        }

        public void Dispose() {
            _cache.Dispose();
            foreach(FileStreamInfo fileStreamInfo in _fileStreams) {
                fileStreamInfo.Stream.Dispose();
            }
        }
    }
}
