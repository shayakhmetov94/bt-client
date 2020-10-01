using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace bittorrent_service.Db
{
    public interface ISqlDbConnectionFactory
    {
        IDbConnection GetConnection();
    }
}
