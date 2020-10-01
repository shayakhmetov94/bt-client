using bittorrent_service.Db;
using System;
using System.Collections.Generic;
using System.Text;
using Dapper;
using System.Linq;

namespace bittorrent_service
{
    class Main_t {
        public static void Main(string[] args) {
            using (SQLiteDataContext sqlDataCtx = new SQLiteDataContext(new SQLiteDbConnectionFactory("Data Source=db.db;Version=3;"))) {

                for (int i = 0; i < 20; i++) {
                    long? lastId = null;
                    var page = sqlDataCtx.List(lastId:lastId);
                    foreach(var item in page) {
                        lastId = item.Id;
                        Console.WriteLine($"lastId = {lastId}, Title = { item.Title}");
                    }
                    
                }
            }

            
        }
    }
}
