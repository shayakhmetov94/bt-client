using bittorrent_client.Base.Announce;
using bittorrent_client.Base.Peers;
using MockHttpServer;
using NUnit.Framework;
using System.Collections.Generic;
using System.Text;

namespace Bittorrent_tests
{
    [TestFixture]
    class AnnouncerTests
    {
        private int _trackerMockPort;
        private MockServer _trackerMock;

        public AnnouncerTests() {
            _trackerMockPort = 55555;
            _trackerMock = new MockServer(_trackerMockPort, "",
                (rq, rs, prm) => {
                    byte[] asciiBytes = Encoding.ASCII.GetBytes(@"d8:completei2187e12:crypto_flags50:                                                  10:incompletei41e8:intervali1800e5:peers300:	 SöÀYfÉšÈÕ¼¥Ô<ÈÕr§†ÈÕ.#±dÈÕ%0YéÎ¤KVünÈÕTøX”ÈÕÃÎiÒ€T%x«TÈÕ–eåËÈÕJ ¥:ÈÕ¹©+Çÿá.¦AóHX@ÕIÈÕEqFÀÈÕ´“ŽÕ#'g@aÈÕÀƒ,iè÷ƒ¤Ó¥ÈÕœAñÏ½dyÊ¼Ýk¨Añ3f·ˆ9Ãz¦ç>…ÂŠæ%»FÙLÁhÐÜÈÕ2#Fù¼uN#|(ÈÕed¯?Þ7Xv¨R¯2{ÊBhDeÎÈÕmä5WÈÕ.k‘âÈÕ3­¶žPcY{É,Q«*ã*2à®á†³ëWÖ2ÁÊQ8d2'¤AñY¤ïÊï2[yÜÜñÂ#sÍ-¹8Þ!­Ôã¿ÈÕÆSXÈÕÅSÈÊÈÕe");
                    rs.OutputStream.Write(asciiBytes, 0, asciiBytes.Length);
                    rs.ContentLength64 = asciiBytes.LongLength;
                    return null;
                });
        }

        [Test]
        public void QueryTest() {
            Announcer announcer = new Announcer(new TestBtClient(), $"http://localhost:{_trackerMockPort}");
            announcer.Bencoder.Encoding = Encoding.ASCII;
            Assert.IsTrue(announcer.Query(out List<Peer> peers, Announcer.ClientEvent.Started));
            
            Assert.AreEqual(announcer.Leechers, 41);
            Assert.AreEqual(announcer.ReAnnounceInterval, 800);
            Assert.AreEqual(announcer.RegularInterval, 1800);
            Assert.AreEqual(announcer.Seeders, 2181);

            List<Peer> expectedPeers = new List<Peer>() {
                new Peer("5.9.32.83", 16191),
                new Peer("89.102.63.63", 16191),
                new Peer("63.63.63.60", 16191),
                new Peer("114.63.28.63", 16191),
                new Peer("46.35.63.100", 16191),
                new Peer("37.48.89.63", 16191),
                new Peer("75.86.63.110", 16191),
                new Peer("84.63.88.63", 16191),
                new Peer("63.63.105.63", 16212),
                new Peer("37.120.63.84", 16191),
                new Peer("63.101.63.63", 16191),
                new Peer("74.32.63.58", 16191),
                new Peer("63.63.43.63", 16191),
                new Peer("46.63.63.65", 16200),
                new Peer("88.64.63.73", 16191),
                new Peer("69.113.70.63", 16191),
                new Peer("63.63.63.63", 8999),
                new Peer("103.11.64.97", 16191),
                new Peer("63.63.44.105", 16191),
                new Peer("63.63.63.63", 16191),
                new Peer("14.2.63.14", 16703),
                new Peer("63.63.29.100", 31039),
                new Peer("63.63.107.63", 16703),
                new Peer("51.15.4.102", 6719),
                new Peer("63.57.63.122", 16191),
                new Peer("62.63.63.63", 6719),
                new Peer("37.63.20.70", 16204),
                new Peer("63.104.63.63", 16191),
                new Peer("50.35.70.63", 16245),
                new Peer("78.35.124.40", 16191),
                new Peer("101.100.63.63", 16183),
                new Peer("88.118.63.11", 21055),
                new Peer("50.123.22.63", 17000),
                new Peer("68.7.101.63", 16191),
                new Peer("109.63.53.87", 16191),
                new Peer("46.107.63.63", 16191),
                new Peer("51.15.63.30", 16191),
                new Peer("80.99.89.123", 16172),
                new Peer("81.63.17.42", 16170),
                new Peer("50.63.63.63", 6719),
                new Peer("63.19.63.63", 22335),
                new Peer("50.63.63.81", 14436),
                new Peer("50.39.63.63", 16703),
                new Peer("89.63.63.63", 16178),
                new Peer("91.121.63.63", 16135),
                new Peer("63.35.115.5", 16173),
                new Peer("63.56.20.15", 16161),
                new Peer("63.63.63.63", 16191),
                new Peer("63.27.83.88", 16191),
                new Peer("63.83.63.63", 16191)
            };

            CollectionAssert.AreEqual(expectedPeers, peers);
        }
        
    }
}
