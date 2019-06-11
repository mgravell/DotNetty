// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.
namespace DotNetty.Transport.Channels.Sockets
{
    using System.Net;
    using System.Net.NetworkInformation;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    public class DatagramSubChannel : SubChannel, IDatagramChannel
    {
        public DatagramSubChannel(IChannelHandlerContext ctx)
            : base(ctx)
        {
            
        }

        public new IDatagramChannel Parent => (IDatagramChannel)base.Parent;

        public bool IsConnected() => this.Parent.IsConnected();
        
        public Task JoinGroup(IPEndPoint multicastAddress)
        {
            return this.Parent.JoinGroup(multicastAddress);
        }

        public Task JoinGroup(IPEndPoint multicastAddress, TaskCompletionSource promise)
        {
            return this.Parent.JoinGroup(multicastAddress, promise);
        }

        public Task JoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface)
        {
            return this.Parent.JoinGroup(multicastAddress, networkInterface);
        }

        public Task JoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, TaskCompletionSource promise)
        {
            return this.Parent.JoinGroup(multicastAddress, networkInterface, promise);
        }

        public Task JoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source)
        {
            return this.Parent.JoinGroup(multicastAddress, networkInterface, source);
        }

        public Task JoinGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source, TaskCompletionSource promise)
        {
            return this.Parent.JoinGroup(multicastAddress, networkInterface, source, promise);
        }

        public Task LeaveGroup(IPEndPoint multicastAddress)
        {
            return this.Parent.LeaveGroup(multicastAddress);
        }

        public Task LeaveGroup(IPEndPoint multicastAddress, TaskCompletionSource promise)
        {
            return this.Parent.LeaveGroup(multicastAddress, promise);
        }

        public Task LeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface)
        {
            return this.Parent.LeaveGroup(multicastAddress, networkInterface);
        }

        public Task LeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, TaskCompletionSource promise)
        {
            return this.Parent.LeaveGroup(multicastAddress, networkInterface, promise);
        }

        public Task LeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source)
        {
            return this.Parent.LeaveGroup(multicastAddress, networkInterface, source);
        }

        public Task LeaveGroup(IPEndPoint multicastAddress, NetworkInterface networkInterface, IPEndPoint source, TaskCompletionSource promise)
        {
            return this.Parent.LeaveGroup(multicastAddress, networkInterface, source, promise);
        }
    }
}