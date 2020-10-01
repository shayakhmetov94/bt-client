using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.Resources;
using System.Text;

namespace bittorrent_service.Base.Db
{
    public class SQLiteDbConnectionFactory : ISqlDbConnectionFactory
    {
        private string _connectionString;
        public SQLiteDbConnectionFactory(string conString) {
            if(string.IsNullOrEmpty(conString)) {
                throw new ArgumentException();
            }

            _connectionString = conString;
        }

        public IDbConnection GetConnection() {
            return new SQLiteConnection(_connectionString);
        }
    }
}
