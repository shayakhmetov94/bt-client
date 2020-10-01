using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace StreamingTest
{
    /// <summary>
    /// Реализует синхронизированный пул ресурсов
    /// </summary>
    /// <typeparam name="T">Тип элемента пула</typeparam>
    class SocketPool : IDisposable
    {
        /// <summary>
        /// Количество выданных элементов
        /// </summary>
        int acquired;

        /// <summary>
        /// Количество загруженных элементов
        /// </summary>
        int count;

        /// <summary>
        /// 
        /// </summary>
        ThreadLocal<Socket> socks;

        public SocketPool()
        {
            socks = new ThreadLocal<Socket>();
        }

        public Socket Acquire()
        {
            return null;
        }




        public void Dispose()
        {
            throw new NotImplementedException();
        }
    }
}
