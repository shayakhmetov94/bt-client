using System;
using System.Collections.Generic;
using System.Text;

namespace bittorrent_service
{
    public class TorrentSession
    {
        public long Id { get; set; }
        public int EncCodePage { get; set; }
        public string Title { get; set; }
        public string Path { get; set; }
        public bool IsActive { get; set; }
        public string PiecesHave { get; set; }
        public string Metainfo { get; set; }
        public string LastValidated { get; private set; }
        public DateTime LastValidatedDate {
            get {
                return _lastValidatedDate;
            }
            set {
                _lastValidatedDate = value;
                if (value == null) {
                    LastValidated = null;
                } else {
                    LastValidated = value.ToString("s", System.Globalization.CultureInfo.InvariantCulture);
                }
            }
        }
        private DateTime _lastValidatedDate { get; set; }
    }
}
