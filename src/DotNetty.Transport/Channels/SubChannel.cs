// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Transport.Channels
{
    using System;
    using System.Net;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public class SubChannel : IChannel
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<SubChannel>();

        sealed class SubChannelId : IChannelId 
        {
            static readonly long serialVersionUID = 1L;
            readonly SubChannel channel;
            

            public SubChannelId(SubChannel channel)
            {
                this.channel = channel;
            }

            
            public int CompareTo(IChannelId o) => this.channel.Parent.Id.CompareTo(o);

            public string AsShortText() => this.channel.Parent.Id.AsShortText();

            public string AsLongText() => this.channel.Parent.Id.AsLongText();
        }

        sealed class SubChannelPipeline : DefaultChannelPipeline
        {
            readonly SubChannel channel;
            
            public SubChannelPipeline(SubChannel channel)
                : base(channel)
            {
                this.channel = channel;
            }
            /*
            @Override
            protected void incrementPendingOutboundBytes(long size) {
                // Do nothing for now
            }

            @Override
            protected void decrementPendingOutboundBytes(long size) {
                // Do nothing for now
            }*/

            protected override void OnUnhandledInboundException(Exception cause) {
                this.channel.ctx.FireExceptionCaught(cause);
            }

            protected override void OnUnhandledInboundChannelActive() {
                this.channel.ctx.FireChannelActive();
            }

            protected override void OnUnhandledInboundChannelInactive() {
                this.channel.ctx.FireChannelInactive();
            }

            protected override void OnUnhandledInboundMessage(object msg) {
                this.channel.ctx.FireChannelRead(msg);
            }

            protected override void OnUnhandledInboundChannelReadComplete() {
                this.channel.ctx.FireChannelReadComplete();
            }

            protected override void OnUnhandledInboundUserEventTriggered(object evt) {
                this.channel.ctx.FireUserEventTriggered(evt);
            }

            protected override void OnUnhandledChannelWritabilityChanged() {
                this.channel.ctx.FireChannelWritabilityChanged();
            }
        }

        class SubChannelUnsafe : IChannelUnsafe
        {
            readonly SubChannel channel;

            //readonly VoidChannelPromise unsafeVoidPromise = new VoidChannelPromise(SubChannel.this, false);
            bool closeInitiated;

            public SubChannelUnsafe(SubChannel channel)
            {
                this.channel = channel;
            }

            public IRecvByteBufAllocatorHandle RecvBufAllocHandle => this.channel.Parent.Unsafe.RecvBufAllocHandle;

            public Task RegisterAsync(IEventLoop eventLoop)
            {
                if (this.channel.registered)
                {
                    throw new NotSupportedException("Re-register is not supported");
                }

                this.channel.registered = true;

                this.channel.Pipeline.FireChannelRegistered();

                return TaskEx.Completed;
            }

            public Task DeregisterAsync() => this.FireChannelInactiveAndDeregister(false);

            public Task BindAsync(EndPoint localAddress)
            {
                return this.channel.ctx.BindAsync(localAddress);
                //ctx.bind(localAddress).addListener(new PromiseRelay(promise));
            }

            public Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress)
            {
                return this.channel.ctx.ConnectAsync(remoteAddress, localAddress); //.addListener(new PromiseRelay(promise));
            }

            public Task DisconnectAsync() => this.channel.ctx.DisconnectAsync();

            public Task CloseAsync()
            {
                if (this.closeInitiated)
                {
                    return this.channel.CloseCompletion;
                    /*if (this.channel.CloseCompletion.isDone()) 
                    {
                        // Closed already.
                        promise.setSuccess();
                    } else if (!(promise is VoidChannelPromise)) 
                    { 
                        // Only needed if no VoidChannelPromise.
                        // This means close() was called before so we just register a listener and
                        // return
                        this.channel.CloseCompletion.addListener(new ChannelFutureListener() {
                            @Override
                            public void operationComplete(ChannelFuture future) {
                            promise.setSuccess();
                        }
                        });
                    }
                    return;*/
                }

                this.closeInitiated = true;

                this.FireChannelInactiveAndDeregister(this.channel.Active);
                return this.channel.ctx.CloseAsync();
            }

            public void CloseForcibly() => this.CloseAsync();

            public void BeginRead() => this.channel.ctx.Read();

            public ValueTask WriteAsync(object message) => this.channel.ctx.WriteAsync(message);

            public void Flush() => this.channel.ctx.Flush();

            public ChannelOutboundBuffer OutboundBuffer =>
                // Always return null as we not use the ChannelOutboundBuffer and not even
                // support it.
                null;

            Task FireChannelInactiveAndDeregister(bool fireChannelInactive)
            {
                if (!this.channel.registered)
                {
                    return TaskEx.Completed;
                }
                
                var tcs = new TaskCompletionSource();

                // As a user may call deregister() from within any method while doing processing
                // in the ChannelPipeline,
                // we need to ensure we do the actual deregister operation later. This is
                // necessary to preserve the
                // behavior of the AbstractChannel, which always invokes channelUnregistered and
                // channelInactive
                // events 'later' to ensure the current events in the handler are completed
                // before these events.
                //
                // See:
                // https://github.com/netty/netty/issues/4435
                this.InvokeLater(
                    () =>
                    {
                        if (fireChannelInactive)
                        {
                            this.channel.pipeline.FireChannelInactive();
                        }

                        ;
                        // The user can fire `deregister` events multiple times but we only want to fire
                        // the pipeline event if the IChannel was actually registered.
                        if (this.channel.registered)
                        {
                            this.channel.registered = false;
                            this.channel.pipeline.FireChannelUnregistered();
                        }
    
                        Util.SafeSetSuccess(tcs, Logger);
                    });

                return tcs.Task;
            }
            
            void InvokeLater(Action task)
            {
                try
                {
                    // This method is used by outbound operation implementations to trigger an inbound event later.
                    // They do not trigger an inbound event immediately because an outbound operation might have been
                    // triggered by another inbound event handler method.  If fired immediately, the call stack
                    // will look like this for example:
                    //
                    //   handlerA.inboundBufferUpdated() - (1) an inbound handler method closes a connection.
                    //   -> handlerA.ctx.close()
                    //      -> channel.unsafe.close()
                    //         -> handlerA.channelInactive() - (2) another inbound handler method called while input (1) yet
                    //
                    // which means the execution of two inbound handler methods of the same handler overlap undesirably.
                    this.channel.EventLoop.Execute(task);
                }
                catch (RejectedExecutionException e)
                {
                    Logger.Warn("Can't invoke task later as EventLoop rejected it", e);
                }
            }
        }        
        
        readonly IChannelId channelId;
        readonly SubChannelUnsafe channelUnsafe;
        readonly IChannelPipeline pipeline;

        protected readonly IChannelHandlerContext ctx;

        volatile bool registered;

        public SubChannel(IChannelHandlerContext ctx) 
        {
            this.ctx = ctx;
            this.channelId = new SubChannelId(this);
            this.channelUnsafe = new SubChannelUnsafe(this);
            this.pipeline = new SubChannelPipeline(this);
        }
        
        public ChannelMetadata Metadata => this.Parent.Metadata;

        public IChannelConfiguration Configuration => this.Parent.Configuration;

        public bool Open => this.Parent.Open;

        public bool Active => this.Parent.Active;

        public bool IsWritable => this.Parent.IsWritable;

        public IChannelId Id => this.channelId;

        public IEventLoop EventLoop => this.Parent.EventLoop;
        
        public virtual IChannel Parent => this.ctx.Channel;
        
        public bool Registered => this.registered;

        public EndPoint LocalAddress => this.Parent.LocalAddress;

        public EndPoint RemoteAddress => this.Parent.RemoteAddress;

        public Task CloseCompletion => this.Parent.CloseCompletion;

        
        /*public long bytesBeforeUnwritable() {
            return Parent.bytesBeforeUnwritable();
        }

        public long bytesBeforeWritable() {
            return Parent.bytesBeforeWritable();
        }*/

        public IChannelUnsafe Unsafe => this.channelUnsafe;

        public IChannelPipeline Pipeline => this.pipeline;

        public IByteBufferAllocator Allocator => this.Configuration.Allocator;
        
        
        public IChannel Read() 
        {
            this.Pipeline.Read();
            return this;
        }

        public IChannel Flush() 
        {
            this.Pipeline.Flush();
            return this;
        }

        public Task BindAsync(EndPoint localAddress) => this.Pipeline.BindAsync(localAddress);

        public Task ConnectAsync(EndPoint remoteAddress) => this.Pipeline.ConnectAsync(remoteAddress);

        public Task ConnectAsync(EndPoint remoteAddress, EndPoint localAddress) => this.Pipeline.ConnectAsync(remoteAddress, localAddress);

        public Task DisconnectAsync() => this.Pipeline.DisconnectAsync();

        public Task CloseAsync() => this.Pipeline.CloseAsync();

        public Task DeregisterAsync() => this.Pipeline.DeregisterAsync();

        public ValueTask WriteAsync(object msg) => this.Pipeline.WriteAsync(msg);

        public Task WriteAndFlushAsync(object msg) => this.Pipeline.WriteAndFlushAsync(msg);

        public ValueTask WriteAndFlushAsync(object msg, bool notifyComplete)  => this.Pipeline.WriteAndFlushAsync(msg, notifyComplete);

    /*    @Override
        public ChannelPromise newPromise() {
            return this.Pipeline.newPromise();
        }

        @Override
        public ChannelProgressivePromise newProgressivePromise() {
            return this.Pipeline.newProgressivePromise();
        }

        @Override
        public Task newSucceededFuture() {
            return this.Pipeline.newSucceededFuture();
        }

        @Override
        public Task newFailedFuture(Exception cause) {
            return this.Pipeline.newFailedFuture(cause);
        }

        @Override
        public ChannelPromise voidPromise() {
            return this.Pipeline.voidPromise();
        }*/

        public IAttribute<T> GetAttribute<T>(AttributeKey<T> key) where T : class
            => this.Parent.GetAttribute(key);

        public bool HasAttribute<T>(AttributeKey<T> key)
            where T : class
            => this.Parent.HasAttribute(key);
        
        public override int GetHashCode() => this.Id.GetHashCode();

        public override bool Equals(object o) => this == o;

        public int CompareTo(IChannel o) {
            if (this == o) {
                return 0;
            }

            return this.Id.CompareTo(o.Id);
        }

        public override string ToString() => this.Parent + "(subchannel)";
    }
}