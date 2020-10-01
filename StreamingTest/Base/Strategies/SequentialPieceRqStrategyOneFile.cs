using bittorrent_client.Base.Util;
using System;
using System.Linq;

namespace bittorrent_client.Base.Strategies
{
    class SequentialPieceRqStrategyOneFile : IRequestStrategy
    {
        private int _piecesCount;
        private int _initialOffset;

        protected BitField _pieces;

        public SequentialPieceRqStrategyOneFile(Metainfo meta, string fileName) {
            Metainfo.File file = FindFileInMeta(meta, fileName);
            _initialOffset = CalcFileStartOffset(meta, file) / meta.PieceLength;
            _pieces = new BitField(meta.PiecesCount);

            _piecesCount = file.Length / meta.PieceLength;
            for (int i = _initialOffset; i < _piecesCount; i++) {
                _pieces[i] = false;
            }
        }

        private Metainfo.File FindFileInMeta(Metainfo meta, string fileName) {
            foreach (var file in meta.Files) {
                if (file.Path.Last().Contains(fileName)) {
                    return file;
                }
            }

            throw new System.IO.FileNotFoundException($"File {fileName} does not found");
        }

        private int CalcFileStartOffset(Metainfo meta, Metainfo.File file) {
            int offset = 0;
            foreach(var metaFile in meta.Files) {
                if (file.Path.Last().Equals(metaFile.Path.Last())) {
                    break;
                }

                offset += file.Length;
            }

            return offset;
        }

        public int Next() {
            int next = _pieces.FirstFalse();
            if (next < 0) {
                return -1;
            }

            _pieces[next] = true;
            return next;
        }

        public void Reset(int pos) {
            if (pos < _initialOffset || pos >= _piecesCount) {
                throw new ArgumentOutOfRangeException("pos");
            }

            _pieces[pos] = false;
        }
    }
}
