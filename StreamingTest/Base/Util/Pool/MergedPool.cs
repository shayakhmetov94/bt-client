using bittorrent_client.Base.Strategies;
using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;

namespace bittorrent_client.Pool
{
    class MergedPool<T> : IResourcePool<T>
    {
        //TODO: cancel acquire by passing null
        private BlockingCollection<T> _myPool;

        private ConcurrentDictionary<int, Task> _task2Pool;
        private CancellationTokenSource _tasksCancelTknSource;

        private IResourcePool<T>[] _pools;

        public MergedPool(IResourcePool<T>[] pools) {
            _myPool = new BlockingCollection<T>();
            _task2Pool = new ConcurrentDictionary<int, Task>();
            _tasksCancelTknSource = new CancellationTokenSource();

            _pools = pools;
        }

        public void Dispose(T resource) {
            //GetPool(resource).Dispose(resource);
        }

        public void Realese(T resource) {
            //GetPool(resource).Realese(resource);
        }

        private void TryTakeFromPools(CancellationToken? cancelToken = null) {
            CancellationToken cancellationToken = cancelToken == null ? _tasksCancelTknSource.Token : cancelToken.Value;
            for(int i = 0; i < _pools.Length; i++) {
                if(_task2Pool.ContainsKey(i)) {
                    var poolTask = _task2Pool[i];
                    if(poolTask.Status != TaskStatus.Running) {
                        TakeFromPool_RegisterAndInvoke(i, _pools[i], cancellationToken);
                    }
                } else {
                    TakeFromPool_RegisterAndInvoke(i, _pools[i], cancellationToken);
                }
            }
        }

        private void TakeFromPool_RegisterAndInvoke(int poolId, IResourcePool<T> pool, CancellationToken cancelToken) {
            _task2Pool.AddOrUpdate(poolId, Task.Factory.StartNew(TakeFromPoolObj, new Tuple<IResourcePool<T>, CancellationToken>(pool, cancelToken)), (k, v) => v);
        }

        private void TakeFromPoolObj(object poolAndCancelTokenTupleObj) {
            Tuple<IResourcePool<T>, CancellationToken> poolAndCancelTokenTuple = (Tuple<IResourcePool<T>, CancellationToken>)poolAndCancelTokenTupleObj;
            TakeFromPool(poolAndCancelTokenTuple.Item1, poolAndCancelTokenTuple.Item2);
        }

        private void TakeFromPool(IResourcePool<T> pool, CancellationToken cancelToken) {
            try {
                T res = pool.Acquire(cancelToken);
                _myPool.Add(res);
            } catch(OperationCanceledException) { }    
        }

        public T Acquire() {
            if(_myPool.Count == 0) {
                TryTakeFromPools();
            }

            return _myPool.Take();
        }

        public T Acquire(CancellationToken token) {
            if(_myPool.Count == 0) {
                TryTakeFromPools(token);
            }
            var res = _myPool.Take(token);

            if(res == null) {
                Debug.WriteLine($"MergedPool: breaking lock");
            }

            return res;
        }

        public void Stop() {
            _tasksCancelTknSource.Cancel();
        }
    }
}
