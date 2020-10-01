using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Security.Cryptography;
using Bittorrent;
using System.Text;
using System.Net;
using System.Linq;
using commons_bittorrent.Base.Bencoding;

namespace bittorrent_client.Base
{
    public class Metainfo
    {
        #region Internal structs
        
        public struct File
        {
            public int Length { get; set; }
            public string Md5Sum { get; set; }
            public List<string> Path { get; set; }
            public string Name { get; set; }
        }

        #endregion

        #region Props and constants

        private const string PieceLengthKey = "piece length";
        private const string InfoDictionaryKey = "info";
        private const string PiecesKey = "pieces";
        private const string FilesKey = "files";
        private const string LengthKey = "length";
        private const string MD5Key = "md5sum";
        private const string PathKey = "path";
        private const string NameKey = "name";
        private const string AnnounceListKey = "announce-list";
        private const string NodesKey = "nodes";
        private const string AnnounceKey = "announce";

        private readonly Bencoder _bencoder;
        private readonly Encoding _encoding;

        public readonly byte[] Protocol;
        public int PieceLength { get; }
        public byte[] InfoHash { get; }
        public int TotalLength { get; }
        public List<File> Files { get; }
        public byte[] PiecesHash { get; }
        public int PiecesCount { get; }
        public List<string> Announces { get; }
        public List<IPEndPoint> Nodes { get; }

        #endregion

        public Metainfo(byte[] data, Encoding encoding) {
            Protocol = encoding.GetBytes("BitTorrent protocol");
            _encoding = encoding;
            _bencoder = new Bencoder();
            var metainfoDict = (Dictionary<string, object>)_bencoder.DecodeElement(data);
            var infoDict = (Dictionary<string, object>)metainfoDict[InfoDictionaryKey];

            PieceLength = (int)infoDict[PieceLengthKey];
            PiecesHash = (byte[])infoDict[PiecesKey];
            InfoHash = CalcInfoHashBytes(infoDict);
            Nodes = GetNodes(metainfoDict);
            Announces = GetAnnounces(metainfoDict);
            if (metainfoDict.ContainsKey(AnnounceKey)) {
                Announces.Add(_bencoder.Encoding.GetString((byte[])metainfoDict[AnnounceKey]));
            }
            Files = GetFiles(infoDict);
            TotalLength = CalcTotalLength(Files);
            PiecesCount = (int)Math.Ceiling((double)(TotalLength / PieceLength));
        }

        private byte[] CalcInfoHashBytes(dynamic infoDict) {
            byte[] encodedInfo = _bencoder.EncodeElement(infoDict);
            var sha1Algo = SHA1.Create();
            return sha1Algo.ComputeHash(encodedInfo);
        }

        private int CalcTotalLength(List<File> files) {
            int totalLength = 0;
            foreach (var file in files)
                totalLength += file.Length;

            return totalLength;
        }

        private List<File> GetFiles(dynamic infoDict) {
            List<File> files = new List<File>(1);
            if (infoDict.ContainsKey(FilesKey)) {
                var filesInfo = (List<dynamic>)infoDict[FilesKey];
                foreach (var fileInfo in filesInfo) {
                    File file = new File();
                    file.Length = fileInfo[LengthKey];

                    if (infoDict.ContainsKey(MD5Key))
                        file.Md5Sum = fileInfo[MD5Key];

                    file.Path = ((List<object>)fileInfo[PathKey]).Select((path) => (string)path).ToList();
                    files.Add(file);
                }
            } else {
                File file = new File
                {
                    Length = infoDict[LengthKey]
                };

                if (infoDict.ContainsKey(MD5Key)) {
                    file.Md5Sum = infoDict[MD5Key];
                }

                if (infoDict.ContainsKey(PathKey)) {
                    file.Path = (List<string>)_bencoder.DecodeElement(_encoding.GetBytes(infoDict[PathKey]));
                }
                if (infoDict.ContainsKey(NameKey)) {
                    file.Name = _bencoder.Encoding.GetString(infoDict[NameKey]);
                }

                files.Add(file);
            }

            return files;
        }

        private List<IPEndPoint> GetNodes(dynamic meta) {
            List<IPEndPoint> nodeEndPoints = new List<IPEndPoint>();

            if (meta.ContainsKey(NodesKey)) {
                List<List<string>> nodesInfo = meta[NodesKey];

                foreach (var nodeInfo in nodesInfo) {
                    nodeEndPoints.Add(new IPEndPoint(IPAddress.Parse(nodeInfo[0]), int.Parse(nodeInfo[1])));
                }
            }

            return nodeEndPoints;
        }

        private List<string> GetAnnounces(Dictionary<string, object> meta) {
            List<string> announces = new List<string>();
            if (!meta.ContainsKey(AnnounceListKey)) {
                return announces;
            }

            List<object> announceList = (List<object>)meta[AnnounceListKey];
            foreach (object announceData in announceList) {
                List<object> innerList = (List<object>)announceData;
                announces.Add(_bencoder.Encoding.GetString((byte[])innerList[0]));
            }

            return announces;
        }
    }
}