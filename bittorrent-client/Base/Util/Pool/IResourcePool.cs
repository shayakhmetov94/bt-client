using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace bittorrent_client.Base.Strategies
{
    public interface IResourcePool<T> {
        void Realese( T resource );
        T Acquire();
        T Acquire(CancellationToken token);
        void Dispose(T resource);
    }
}
