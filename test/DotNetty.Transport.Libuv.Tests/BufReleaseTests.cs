// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Libuv.Tests
{
    using System;
    using System.Diagnostics;
    using System.Globalization;
    using System.Net;
    using System.Runtime.CompilerServices;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Tests.Common;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using Xunit;

    using static TestUtil;

    [Collection(LibuvTransport)]
    public sealed class BufReleaseTests : IDisposable
    {
        readonly IEventLoopGroup group;
        IChannel serverChannel;
        IChannel clientChannel;

        public BufReleaseTests()
        {
            this.group = new EventLoopGroup(1);
        }

        [Fact]
        public async Task BufRelease()
        {
            ServerBootstrap sb = new ServerBootstrap()
                .Group(this.group)
                .Channel<TcpServerChannel>();
            Bootstrap cb = new Bootstrap()
                .Group(this.group)
                .Channel<TcpChannel>();
            await this.BufRelease(sb, cb);
        }

        async Task BufRelease(ServerBootstrap sb, Bootstrap cb)
        {
            var serverHandler = new BufWriterHandler();
            var clientHandler = new BufWriterHandler();

            sb.ChildHandler(serverHandler);
            cb.Handler(clientHandler);

            // start server
            Task<IChannel> task = sb.BindAsync(LoopbackAnyPort);
            await task;//Assert.True(task.Wait(DefaultTimeout), "Server bind timed out");
            this.serverChannel = task.Result;
            Assert.NotNull(this.serverChannel.LocalAddress);
            var endPoint = (IPEndPoint)this.serverChannel.LocalAddress;

            // connect to server
            task = cb.ConnectAsync(endPoint);
            //Assert.True(task.Wait(DefaultTimeout), "Connect to server timed out");
            await task;
            this.clientChannel = task.Result;
            Assert.NotNull(this.clientChannel.LocalAddress);

            // Ensure the server socket accepted the client connection *and* initialized pipeline successfully.
            //Assert.True(serverHandler.Added.Wait(DefaultTimeout), "Channel HandlerAdded timed out");
            await serverHandler.Added;

            // and then close all sockets.
            //this.serverChannel.CloseAsync().Wait(DefaultTimeout);
            await this.serverChannel.CloseAsync();
            //this.clientChannel.CloseAsync().Wait(DefaultTimeout);
            await this.clientChannel.CloseAsync();

            await serverHandler.Check();
            await clientHandler.Check();

            serverHandler.Release();
            clientHandler.Release();
        }

        sealed class BufWriterHandler : SimpleChannelInboundHandler<object>
        {
            readonly Random random;
            readonly TaskCompletionSource completion;

            IByteBuffer buf;
            readonly TaskCompletionSource writeCompletion;

            public BufWriterHandler()
            {
                this.random = new Random();
                this.completion = new TaskCompletionSource();
                this.writeCompletion = new TaskCompletionSource();
            }

            public Task Added => this.completion.Task;

            public override void HandlerAdded(IChannelHandlerContext context)
            {
                this.completion.TryComplete();
            }

            public override async void ChannelActive(IChannelHandlerContext ctx)
            {
                var data = new byte[1024];
                this.random.NextBytes(data);

                this.buf = ctx.Allocator.Buffer();
                // call retain on it so it can't be put back on the pool
                this.buf.WriteBytes(data).Retain();

                await ctx.Channel.WriteAndFlushAsync(this.buf);
                this.writeCompletion.TryComplete();
            }

            protected override void ChannelRead0(IChannelHandlerContext ctx, object msg)
            {
                // discard
            }

            public async Task Check()
            {
                //Assert.NotNull(this.writeTask);
                //Assert.True(this.writeTask.Wait(DefaultTimeout), "Write task timed out");
                await this.writeCompletion.Task;
                Assert.Equal(1, this.buf.ReferenceCount);
            }
            
            public void Release()
            {
                this.buf.Release();
            }
        }

        public void Dispose()
        {
            this.clientChannel?.CloseAsync().Wait(DefaultTimeout);
            this.serverChannel?.CloseAsync().Wait(DefaultTimeout);
            this.group.ShutdownGracefullyAsync(TimeSpan.Zero, TimeSpan.Zero).Wait(DefaultTimeout);
        }
    }
}
