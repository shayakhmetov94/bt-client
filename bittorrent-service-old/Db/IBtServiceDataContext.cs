using System;
using System.Collections.Generic;
using System.Text;

namespace bittorrent_service.Db
{
    public interface IBtServiceDataContext : IDisposable
    {
        bool Add(TorrentSession torrentInfo);
        bool Remove(TorrentSession torrentInfo);
        IEnumerable<TorrentSession> List(bool? activeOnly, int count, long? lastId);
    }
}
