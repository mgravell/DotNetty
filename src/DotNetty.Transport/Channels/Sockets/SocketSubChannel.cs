// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels.Sockets
{
    using System.Threading.Tasks;

    public class SocketSubChannel : SubChannel, ISocketChannel
    {
        public SocketSubChannel(IChannelHandlerContext ctx)
            : base(ctx)
        {
        }

        public new ISocketChannel Parent => (ISocketChannel)base.Parent;

        public bool InputShutdown => this.Parent.InputShutdown;

        public Task ShutdownInputAsync() => this.Parent.ShutdownInputAsync();
        
        public bool OutputShutdown => this.Parent.OutputShutdown;

        public Task ShutdownOutputAsync() => this.Parent.ShutdownOutputAsync();
    }
}