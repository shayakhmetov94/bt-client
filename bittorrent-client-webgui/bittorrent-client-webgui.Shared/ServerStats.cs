using System;
using System.Collections.Generic;
using System.Text;

namespace bittorrent_client_webgui.Shared
{
    public class ServerStats {
        public long SessionsCount { get; set; }
        public long ActiveSessionsCount { get; set; }
        public IEnumerable<SpeedStamp> AverageDownloadSpeed { get; set; }
        public IEnumerable<SpeedStamp> AverageUploadSpeed { get; set; }
    }
}
