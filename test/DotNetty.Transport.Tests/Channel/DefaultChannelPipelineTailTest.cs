// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Tests.Channel
{
    using System;
    using System.IO;
    using System.Net;
    using System.Threading;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Bootstrapping;
    using DotNetty.Transport.Channels;
    using DotNetty.Transport.Channels.Local;
    using Moq;
    using Xunit;

    public class DefaultChannelPipelineTailTest : IDisposable
    {
        readonly IEventLoopGroup group;

        public DefaultChannelPipelineTailTest()
        {
            this.group = new MultithreadEventLoopGroup(1);
        }

        public void Dispose()
        {
            this.group?.ShutdownGracefullyAsync();
        }

        [Fact]
        public async Task TestOnUnhandledInboundChannelActive()
        {
            var latch = new CountdownEvent(1);

            var myChannel = new Mock<MyChannel> { CallBase = true };
            myChannel.Setup(c => c.OnUnhandledInboundChannelActive())
                .Callback(() => latch.Signal());

            var ch = await this.ConnectChannel(myChannel);

            Assert.True(latch.IsSet);
            await ch.CloseAsync();
        }

        [Fact]
        public async Task TestOnUnhandledInboundChannelInactive()
        {
            var latch = new CountdownEvent(1);

            var myChannel = new Mock<MyChannel> { CallBase = true };
            myChannel
                .Setup(c => c.OnUnhandledInboundChannelInactive())
                .Callback(() => { latch.Signal(); });

            await (await this.ConnectChannel(myChannel)).CloseAsync();
            latch.Wait(TimeSpan.FromSeconds(1));
            Assert.True(latch.IsSet);
        }

        [Fact]
        public async Task TestOnUnhandledInboundException()
        {
            Exception cause = null;
            var latch = new CountdownEvent(1);

            var myChannel = new Mock<MyChannel> { CallBase = true };
            myChannel.Setup(c => c.OnUnhandledInboundException(It.IsAny<Exception>()))
                .Callback<Exception>(
                    ex =>
                    {
                        cause = ex;
                        latch.Signal();
                    });

            var channel = await this.ConnectChannel(myChannel);

            try
            {
                var ex = new IOException("testOnUnhandledInboundException");
                channel.Pipeline.FireExceptionCaught(ex);
                latch.Wait(TimeSpan.FromSeconds(1));
                Assert.True(latch.IsSet);
                Assert.Same(ex, cause);
            }
            finally
            {
                await channel.CloseAsync();
            }
        }

        [Fact]
        public async Task TestOnUnhandledInboundMessage()
        {
            var latch = new CountdownEvent(1);
            var myChannel = new Mock<MyChannel> { CallBase = true };
            myChannel.Setup(c => c.OnUnhandledInboundMessage(It.IsAny<object>())).Callback(() => latch.Signal());

            var channel = await this.ConnectChannel(myChannel);

            try
            {
                channel.Pipeline.FireChannelRead("TestOnUnhandledInboundMessage");
                latch.Wait(TimeSpan.FromSeconds(1));
                Assert.True(latch.IsSet);
            }
            finally
            {
                await channel.CloseAsync();
            }
        }

        [Fact]
        public async Task TestOnUnhandledInboundReadComplete()
        {
            var latch = new CountdownEvent(1);
            var myChannel = new Mock<MyChannel> { CallBase = true };
            myChannel.Setup(c => c.OnUnhandledInboundReadComplete()).Callback(() => latch.Signal());

            var channel = await this.ConnectChannel(myChannel);

            try
            {
                channel.Pipeline.FireChannelReadComplete();
                latch.Wait(TimeSpan.FromSeconds(1));
            }
            finally
            {
                await channel.CloseAsync();
            }
        }

        [Fact]
        public async Task TestOnUnhandledInboundUserEventTriggered()
        {
            var latch = new CountdownEvent(1);
            var myChannel = new Mock<MyChannel> { CallBase = true };
            myChannel.Setup(c => c.OnUnhandledInboundUserEventTriggered(It.IsAny<object>())).Callback<object>(_ => latch.Signal());

            var channel = await this.ConnectChannel(myChannel);

            try
            {
                channel.Pipeline.FireUserEventTriggered("testOnUnhandledInboundUserEventTriggered");
                latch.Wait(TimeSpan.FromSeconds(1));
                Assert.True(latch.IsSet);
            }
            finally
            {
                await channel.CloseAsync();
            }
        }

        [Fact]
        public async Task TestOnUnhandledInboundWritabilityChanged()
        {
            var latch = new CountdownEvent(1);
            var myChannel = new Mock<MyChannel> { CallBase = true };

            myChannel.Setup(c => c.OnUnhandledInboundWritabilityChanged()).Callback(() => latch.Signal());

            var channel = await this.ConnectChannel(myChannel);

            try
            {
                channel.Pipeline.FireChannelWritabilityChanged();
                latch.Wait(TimeSpan.FromSeconds(1));
                Assert.True(latch.IsSet);
            }
            finally
            {
                await channel.CloseAsync();
            }
        }

        Task<IChannel> ConnectChannel(Mock<MyChannel> channel)
        {
            var bootstrapper = new Bootstrap()
                .ChannelFactory(() => channel.Object)
                .Group(this.group)
                .Handler(new ChannelHandlerAdapter())
                .RemoteAddress(LocalAddress.Any);

            return bootstrapper.ConnectAsync();
        }

        public abstract class MyChannel : AbstractChannel
        {
            static readonly ChannelMetadata METADATA = new ChannelMetadata(false);

            readonly IChannelConfiguration config;

            bool active;
            bool closed;

            protected MyChannel()
                : base(null)
            {
                this.config = new DefaultChannelConfiguration(this);
            }

            protected override DefaultChannelPipeline NewChannelPipeline() => new MyChannelPipeline(this);

            public override IChannelConfiguration Configuration => this.config;

            public override bool Open => !this.closed;

            public override bool Active => this.Open && this.active;

            public override ChannelMetadata Metadata => METADATA;

            protected override IChannelUnsafe NewUnsafe() => new MyUnsafe(this);

            protected override bool IsCompatible(IEventLoop eventLoop) => true;

            protected override EndPoint LocalAddressInternal => null;

            protected override EndPoint RemoteAddressInternal => null;

            protected override void DoBind(EndPoint localAddress)
            {
            }

            protected override void DoDisconnect()
            {
            }

            protected override void DoClose()
            {
                this.closed = true;
            }

            protected override void DoBeginRead()
            {
            }

            protected override void DoWrite(ChannelOutboundBuffer input) => throw new IOException();

            public virtual void OnUnhandledInboundChannelActive()
            {
            }

            public virtual void OnUnhandledInboundChannelInactive()
            {
            }

            public virtual void OnUnhandledInboundException(Exception cause)
            {
            }

            public virtual void OnUnhandledInboundMessage(object msg)
            {
            }

            public virtual void OnUnhandledInboundReadComplete()
            {
            }

            public virtual void OnUnhandledInboundUserEventTriggered(object evt)
            {
            }

            public virtual void OnUnhandledInboundWritabilityChanged()
            {
            }

            class MyUnsafe : AbstractUnsafe
            {
                readonly MyChannel myChannel;

                public MyUnsafe(MyChannel channel)
                    : base(channel)
                {
                    this.myChannel = channel;
                }

                public override Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
                {
                    var tcs = new TaskCompletionSource();

                    if (!this.EnsureOpen(tcs))
                    {
                        return tcs.Task;
                    }

                    if (!this.myChannel.active)
                    {
                        this.myChannel.active = true;
                        this.myChannel.Pipeline.FireChannelActive();
                    }

                    tcs.SetResult(0);

                    return tcs.Task;
                }
            }

            class MyChannelPipeline : DefaultChannelPipeline
            {
                readonly MyChannel myChannel;

                public MyChannelPipeline(MyChannel channel)
                    : base(channel)
                {
                    this.myChannel = channel;
                }

                protected override void OnUnhandledInboundChannelActive() => this.myChannel.OnUnhandledInboundChannelActive();

                protected override void OnUnhandledInboundChannelInactive() => this.myChannel.OnUnhandledInboundChannelInactive();

                protected override void OnUnhandledInboundException(Exception cause) => this.myChannel.OnUnhandledInboundException(cause);

                protected override void OnUnhandledInboundMessage(object msg) => this.myChannel.OnUnhandledInboundMessage(msg);

                protected override void OnUnhandledInboundChannelReadComplete() => this.myChannel.OnUnhandledInboundReadComplete();

                protected override void OnUnhandledInboundUserEventTriggered(object evt) => this.myChannel.OnUnhandledInboundUserEventTriggered(evt);

                protected override void OnUnhandledChannelWritabilityChanged() => this.myChannel.OnUnhandledInboundWritabilityChanged();
            }
        }
    }
}