using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace bittorrent_client.Base.Strategies
{
    public enum PiecePickStrategy { RarestFirst = 0x01, Random = 0x02, Sequential = 0x03, SequentialOneFile = 0x04 }

    public interface IRequestStrategy
    {
        int Next();
        void Reset(int pos);
    }
}
