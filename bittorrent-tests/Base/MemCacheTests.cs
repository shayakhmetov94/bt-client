using bittorrent_client.Base.Storage;
using NUnit.Framework;
using System.Linq;

namespace Bittorrent_tests
{
    [TestFixture]
    class MemCacheTests
    {
        [Test]
        public void ArePiecesGettingRemoved() {
            int capacity = 3;
            MemCachedPieceStorage storage = new MemCachedPieceStorage(capacity, 1024, 1024 * 4);
            for(int i = 0; i < 100; i++) {
                Assert.IsNotNull(storage.GetPiece(i));
            }

            Assert.IsTrue(capacity == storage.Count());

            storage.Remove(99);
            storage.Remove(98);
            storage.Remove(97);

            Assert.IsTrue(storage.Count() == 0);

            for (int i = 0; i < 10; i++) {
                Assert.IsNotNull(storage.GetPiece(i));
            }

            Assert.IsTrue(capacity == storage.Count());
        }


            
    }
}
