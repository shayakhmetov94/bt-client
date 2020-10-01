using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bittorrent_service_exec
{
    class Program
    {
        static void Main(string[] args) {
            using (SQLiteDataContext sqlDataCtx = new SQLiteDataContext(new SQLiteDbConnectionFactory("Data Source=db.db;Version=3;"))) {
                sqlDataCtx.Add(new TorrentSession()
                {
                    EncCodePage = Encoding.UTF8.CodePage,
                    Title = "Test",
                    Path = "some_path",
                    IsActive = false,
                    PiecesHave = "",
                    Metainfo = "AA",
                    LastValidated = DateTime.Now
                });
            }
        }
    }
}
