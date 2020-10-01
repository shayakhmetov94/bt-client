using bittorrent_service.Models;
using System;
using System.Collections.Generic;
using System.Text;

namespace bittorrent_service.Base.Db
{
    public interface IBtServiceDataContext
    {
        Stats GetStats();
        IEnumerable<SpeedStamp> AverageDownloadSpeed(int count = 50);
        IEnumerable<SpeedStamp> AverageUploadSpeed(int count = 50);
        bool AddSession(TorrentSession torrentInfo);
        bool RemoveSession(long id);
        IEnumerable<TorrentSession> ListSessions(bool? activeOnly, int count, long? lastId);
    }
}
