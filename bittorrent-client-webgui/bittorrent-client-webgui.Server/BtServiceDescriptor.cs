using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace bittorrent_client_webgui.Server
{
    public class BtServiceDescriptor : ServiceDescriptor
    {
        public BtServiceDescriptor(Type serviceType, object instance) : base(serviceType, instance) {
        }

        public BtServiceDescriptor(Type serviceType, Type implementationType, ServiceLifetime lifetime) : base(serviceType, implementationType, lifetime) {
        }
    }
}
