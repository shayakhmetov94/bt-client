using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;
using bittorrent_service.Models;
using Dapper;

namespace bittorrent_service.Base.Db
{
    public class SQLiteDataContext : IBtServiceDataContext
    {
        private ISqlDbConnectionFactory _connectionFactory;

        public SQLiteDataContext(ISqlDbConnectionFactory conFactory) {
            if(conFactory == null) {
                throw new ArgumentNullException(nameof(conFactory));
            }

            _connectionFactory = conFactory;
        }

        public bool AddSession(TorrentSession session) {
            if (session == null) {
                throw new ArgumentNullException(nameof(session));
            }

            using (IDbConnection connection = _connectionFactory.GetConnection()) {
                int isActive = (session.IsActive ? 1 : 0);
                return connection.Execute(
                    $"INSERT INTO {nameof(TorrentSession) } ({nameof(session.EncCodePage)}, " +
                    $" {nameof(session.Title)}, {nameof(session.Path)}, {nameof(session.IsActive)}," +
                    $" {nameof(session.PiecesHave)}, {nameof(session.Metainfo)}, {nameof(session.LastValidated)}  ) VALUES(?,?,?,?,?,?,?)",
                    new
                    {
                        session.EncCodePage,
                        session.Title,
                        session.Path,
                        isActive,
                        session.PiecesHave,
                        session.Metainfo,
                        session.LastValidated
                    }
                ) > 0;
            }
        }

        public Stats GetStats() {
            using (IDbConnection connection = _connectionFactory.GetConnection()) {
                string query = $"SELECT {nameof(Stats.SessionCount)}, {nameof(Stats.ActiveSessionCount)} " +
                    $"FROM {nameof(Stats)} ";

                return connection.Query<Stats>(query).First();
            }
        }

        public IEnumerable<SpeedStamp> AverageDownloadSpeed(int count) {
            using (IDbConnection connection = _connectionFactory.GetConnection()) {
                string query = $"SELECT {nameof(SpeedStamp.UtcTime)}, {nameof(SpeedStamp.Value)} " +
                    $"FROM AVGDOWNLOADSPEED " +
                    $"ORDER BY {nameof(SpeedStamp.UtcTime)}";

                return connection.Query<SpeedStamp>(query);
            }
        }

        public IEnumerable<SpeedStamp> AverageUploadSpeed(int count) {
            using (IDbConnection connection = _connectionFactory.GetConnection()) {
                string query = $"SELECT {nameof(SpeedStamp.UtcTime)}, {nameof(SpeedStamp.Value)} " +
                    $"FROM AVGUPLOADSPEED " +
                    $"ORDER BY {nameof(SpeedStamp.UtcTime)}";

                return connection.Query<SpeedStamp>(query);
            }
        }

        public IEnumerable<TorrentSession> ListSessions(bool? activeOnly = null, int count = 10, long? lastId = null) {
            using (IDbConnection connection = _connectionFactory.GetConnection()) {
                string query =  $"SELECT {nameof(TorrentSession.Id)}, {nameof(TorrentSession.EncCodePage)}, " +
                    $"{nameof(TorrentSession.Title)}, {nameof(TorrentSession.Path)}, " +
                    $"{nameof(TorrentSession.IsActive)}, {nameof(TorrentSession.PiecesHave)}, " +
                    $"{nameof(TorrentSession.Metainfo)}, {nameof(TorrentSession.LastValidated)} " +
                    $"FROM {nameof(TorrentSession)} " +
                    (activeOnly != null ?
                    $"WHERE {nameof(TorrentSession.IsActive)} = {(activeOnly.Value ? "1" : "0")} " : "") +
                    (lastId != null ?
                    $" AND {nameof(TorrentSession.Id)} > ? " : "") +
                    $"ORDER BY {nameof(TorrentSession.Id)} " +
                    $"LIMIT ?";


                return lastId != null ?
                    connection.Query<TorrentSession>(query, new { count, lastId.Value }) :
                    connection.Query<TorrentSession>(query, new { count });
            }
        }

        public bool RemoveSession(long id) {
            using (IDbConnection connection = _connectionFactory.GetConnection()) {
                string delete = $"DELETE * " +
                    $"FROM {nameof(TorrentSession)}" +
                    $"WHERE {nameof(TorrentSession.Id)} = ?";

                return connection.Execute(delete, id) > 0;
            }
        }
    }
}
