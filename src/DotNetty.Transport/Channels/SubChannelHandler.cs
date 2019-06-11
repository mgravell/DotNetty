// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Transport.Channels.Sockets;

    public class SubChannelHandler : ChannelDuplexHandler
    {
        readonly IChannelHandler[] handlers;
        IChannel sc;

        public SubChannelHandler(params IChannelHandler[] handlers)
        {
            this.handlers = handlers;
        }

        public IChannel SubChannel => this.sc;

        public override Task BindAsync(IChannelHandlerContext ctx, EndPoint localAddress) => this.sc.BindAsync(localAddress);

        public override Task ConnectAsync(IChannelHandlerContext ctx, EndPoint remoteAddress, EndPoint localAddress) => this.sc.ConnectAsync(remoteAddress, localAddress);

        public override Task DisconnectAsync(IChannelHandlerContext ctx) => this.sc.DisconnectAsync();

        public override Task CloseAsync(IChannelHandlerContext ctx) => this.sc.CloseAsync();

        public override Task DeregisterAsync(IChannelHandlerContext ctx) => this.sc.DeregisterAsync();

        public override void Read(IChannelHandlerContext ctx) => this.sc.Pipeline.Read();

        public override ValueTask WriteAsync(IChannelHandlerContext ctx, object msg) => this.sc.WriteAsync(msg);

        public override void Flush(IChannelHandlerContext ctx) => this.sc.Flush();

        public override async void ChannelRegistered(IChannelHandlerContext ctx) => await this.sc.Unsafe.RegisterAsync(ctx.Channel.EventLoop);

        public override void ChannelUnregistered(IChannelHandlerContext ctx) => this.sc.DeregisterAsync();

        public override void ChannelActive(IChannelHandlerContext ctx) => this.sc.Pipeline.FireChannelActive();

        public override void ChannelInactive(IChannelHandlerContext ctx) => this.sc.Pipeline.FireChannelInactive();

        public override void ChannelRead(IChannelHandlerContext ctx, object msg) => this.sc.Pipeline.FireChannelRead(msg);

        public override void ChannelReadComplete(IChannelHandlerContext ctx) => this.sc.Pipeline.FireChannelReadComplete();

        public override void UserEventTriggered(IChannelHandlerContext ctx, object evt) => this.sc.Pipeline.FireUserEventTriggered(evt);

        public override void ChannelWritabilityChanged(IChannelHandlerContext ctx) => this.sc.Pipeline.FireChannelWritabilityChanged();

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause) => this.sc.Pipeline.FireExceptionCaught(cause);

        public override bool IsSharable => false;

        protected IChannel CreateSubChannel(IChannelHandlerContext ctx)
        {
            if (ctx.Channel is IDatagramChannel)
            {
                return new DatagramSubChannel(ctx);
            }
            else if (ctx.Channel is ISocketChannel)
            {
                return new SocketSubChannel(ctx);
            }
            else
            {
                return new SubChannel(ctx);
            }
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            this.sc = this.CreateSubChannel(ctx);
            if (this.handlers != null)
            {
                this.sc.Pipeline.AddFirst(this.handlers);
            }
        }

        public override void HandlerRemoved(IChannelHandlerContext ctx)
        {
            IChannelHandler first;
            while ((first = this.sc.Pipeline.First()) != null)
            {
                this.sc.Pipeline.Remove(first);
            }
        }
    }
}