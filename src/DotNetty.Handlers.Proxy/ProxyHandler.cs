// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Handlers.Proxy
{
    using System;
    using System.Diagnostics.Contracts;
    using System.Net;
    using System.Text;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;
    using DotNetty.Transport.Channels;

    public abstract class ProxyHandler : ChannelDuplexHandler
    {
        static readonly IInternalLogger Logger = InternalLoggerFactory.GetInstance<ProxyHandler>();

        /**
     * The default connect timeout: 10 seconds.
     */
        static readonly TimeSpan DefaultConnectTimeout = TimeSpan.FromMilliseconds(10000);

        /**
         * A string that signifies 'no authentication' or 'anonymous'.
         */
        protected const string AuthNone = "none";

        readonly EndPoint proxyAddress;

        volatile EndPoint destinationAddress;
        TimeSpan connectTimeout = DefaultConnectTimeout;

        IChannelHandlerContext ctx;
        PendingWriteQueue pendingWrites;
        bool finished;
        bool suppressChannelReadComplete;
        bool flushedPrematurely;

        readonly TaskCompletionSource<IChannel> connectPromise = new TaskCompletionSource<IChannel>();

        IScheduledTask connectTimeoutFuture;

        protected ProxyHandler(EndPoint proxyAddress)
        {
            Contract.Requires(proxyAddress != null);
            this.proxyAddress = proxyAddress;
        }

        /**
         * Returns the name of the proxy protocol in use.
         */
        public abstract string Protocol { get; }

        /**
         * Returns the name of the authentication scheme in use.
         */
        public abstract string AuthScheme { get; }

        /**
         * Returns the address of the proxy server.
         */
        public EndPoint ProxyAddress => this.proxyAddress;

        /**
         * Returns the address of the destination to connect to via the proxy server.
         */
        public EndPoint DestinationAddress => this.destinationAddress;

        /**
         * Returns {@code true} if and only if the connection to the destination has been established successfully.
         */
        public bool Connected => this.connectPromise.Task.Status == TaskStatus.RanToCompletion;

        /**
         * Returns a {@link Future} that is notified when the connection to the destination has been established
         * or the connection attempt has failed.
         */
        public Task<IChannel> ConnectFuture => this.connectPromise.Task;

        /**
         * Connect timeout.  If the connection attempt to the destination does not finish within
         * the timeout, the connection attempt will be failed.
         */
        public TimeSpan ConnectTimeout
        {
            get => this.connectTimeout;
            set
            {
                if (value <= TimeSpan.Zero)
                {
                    value = TimeSpan.Zero;
                }

                this.connectTimeout = value;
            }
        }

        public override void HandlerAdded(IChannelHandlerContext ctx)
        {
            this.ctx = ctx;

            this.AddCodec(ctx);

            if (ctx.Channel.Active)
            {
                // channelActive() event has been fired already, which means this.channelActive() will
                // not be invoked. We have to initialize here instead.
                this.SendInitialMessage(ctx);
            }
            else
            {
                // channelActive() event has not been fired yet.  this.channelOpen() will be invoked
                // and initialization will occur there.
            }
        }

        /**
         * Adds the codec handlers required to communicate with the proxy server.
         */
        protected abstract void AddCodec(IChannelHandlerContext ctx);

        /**
         * Removes the encoders added in {@link #addCodec(IChannelHandlerContext)}.
         */
        protected abstract void RemoveEncoder(IChannelHandlerContext ctx);

        /**
         * Removes the decoders added in {@link #addCodec(IChannelHandlerContext)}.
         */
        protected abstract void RemoveDecoder(IChannelHandlerContext ctx);

        public override Task ConnectAsync(IChannelHandlerContext context, EndPoint remoteAddress, EndPoint localAddress)
        {
            if (this.destinationAddress != null)
            {
                return TaskEx.FromException(new ConnectionPendingException());
            }

            this.destinationAddress = remoteAddress;

            return this.ctx.ConnectAsync(this.proxyAddress, localAddress);
        }

        public override void ChannelActive(IChannelHandlerContext ctx)
        {
            this.SendInitialMessage(ctx);
            ctx.FireChannelActive();
        }

        /**
         * Sends the initial message to be sent to the proxy server. This method also starts a timeout task which marks
         * the {@link #connectPromise} as failure if the connection attempt does not success within the timeout.
         */
        void SendInitialMessage(IChannelHandlerContext ctx)
        {
            var connectTimeout = this.connectTimeout;
            if (connectTimeout > TimeSpan.Zero)
            {
                this.connectTimeoutFuture = ctx.Executor.Schedule(ConnectTimeout, connectTimeout);
            }

            object initialMessage = this.NewInitialMessage(ctx);
            if (initialMessage != null)
            {
                this.SendToProxyServer(initialMessage);
            }

            ReadIfNeeded(ctx);

            void ConnectTimeout()
            {
                if (!this.connectPromise.Task.IsCompleted)
                {
                    this.SetConnectFailure(new ProxyConnectException(this.ExceptionMessage("timeout")));
                }
            }
        }

        /**
         * Returns a new message that is sent at first time when the connection to the proxy server has been established.
         *
         * @return the initial message, or {@code null} if the proxy server is expected to send the first message instead
         */
        protected abstract object NewInitialMessage(IChannelHandlerContext ctx);

        /**
         * Sends the specified message to the proxy server.  Use this method to send a response to the proxy server in
         * {@link #handleResponse(IChannelHandlerContext, object)}.
         */
        protected void SendToProxyServer(object msg)
        {
            this.ctx.WriteAndFlushAsync(msg).ContinueWith(OnCompleted, TaskContinuationOptions.NotOnRanToCompletion | TaskContinuationOptions.ExecuteSynchronously);

            void OnCompleted(Task future)
            {
                this.SetConnectFailure(future.Exception);
            }
        }

        public override void ChannelInactive(IChannelHandlerContext ctx)
        {
            if (this.finished)
            {
                ctx.FireChannelInactive();
            }
            else
            {
                // Disconnected before connected to the destination.
                this.SetConnectFailure(new ProxyConnectException(this.ExceptionMessage("disconnected")));
            }
        }

        public override void ExceptionCaught(IChannelHandlerContext ctx, Exception cause)
        {
            if (this.finished)
            {
                ctx.FireExceptionCaught(cause);
            }
            else
            {
                // Exception was raised before the connection attempt is finished.
                this.SetConnectFailure(cause);
            }
        }

        public override void ChannelRead(IChannelHandlerContext ctx, object msg)
        {
            if (this.finished)
            {
                // Received a message after the connection has been established; pass through.
                this.suppressChannelReadComplete = false;
                ctx.FireChannelRead(msg);
            }
            else
            {
                this.suppressChannelReadComplete = true;
                Exception cause = null;
                try
                {
                    bool done = this.HandleResponse(ctx, msg);
                    if (done)
                    {
                        this.SetConnectSuccess();
                    }
                }
                catch (Exception t)
                {
                    cause = t;
                }
                finally
                {
                    ReferenceCountUtil.Release(msg);
                    if (cause != null)
                    {
                        this.SetConnectFailure(cause);
                    }
                }
            }
        }

        /**
         * Handles the message received from the proxy server.
         *
         * @return {@code true} if the connection to the destination has been established,
         *         {@code false} if the connection to the destination has not been established and more messages are
         *         expected from the proxy server
         */
        protected abstract bool HandleResponse(IChannelHandlerContext ctx, object response);

        void SetConnectSuccess()
        {
            this.finished = true;

            this.CancelConnectTimeoutFuture();

            if (!this.connectPromise.Task.IsCompleted)
            {
                bool removedCodec = true;

                removedCodec &= this.SafeRemoveEncoder();

                this.ctx.FireUserEventTriggered(
                    new ProxyConnectionEvent(this.Protocol, this.AuthScheme, this.proxyAddress, this.destinationAddress));

                removedCodec &= this.SafeRemoveDecoder();

                if (removedCodec)
                {
                    this.WritePendingWrites();

                    if (this.flushedPrematurely)
                    {
                        this.ctx.Flush();
                    }

                    this.connectPromise.TrySetResult(this.ctx.Channel);
                }
                else
                {
                    // We are at inconsistent state because we failed to remove all codec handlers.
                    Exception cause = new ProxyConnectException(
                        "failed to remove all codec handlers added by the proxy handler; bug?");
                    this.FailPendingWritesAndClose(cause);
                }
            }
        }

        bool SafeRemoveDecoder()
        {
            try
            {
                this.RemoveDecoder(this.ctx);
                return true;
            }
            catch (Exception e)
            {
                Logger.Warn("Failed to remove proxy decoders:", e);
            }

            return false;
        }

        bool SafeRemoveEncoder()
        {
            try
            {
                this.RemoveEncoder(this.ctx);
                return true;
            }
            catch (Exception e)
            {
                Logger.Warn("Failed to remove proxy encoders:", e);
            }

            return false;
        }

        void SetConnectFailure(Exception cause)
        {
            this.finished = true;

            this.CancelConnectTimeoutFuture();

            if (!this.connectPromise.Task.IsCompleted)
            {
                if (!(cause is ProxyConnectException))
                {
                    cause = new ProxyConnectException(this.ExceptionMessage(cause.ToString()), cause);
                }

                this.SafeRemoveDecoder();
                this.SafeRemoveEncoder();
                this.FailPendingWritesAndClose(cause);
            }
        }

        void FailPendingWritesAndClose(Exception cause)
        {
            this.FailPendingWrites(cause);

            this.connectPromise.TrySetException(cause);

            this.ctx.FireExceptionCaught(cause);

            this.ctx.CloseAsync();
        }

        void CancelConnectTimeoutFuture()
        {
            if (this.connectTimeoutFuture != null)
            {
                this.connectTimeoutFuture.Cancel();
                this.connectTimeoutFuture = null;
            }
        }

        /**
         * Decorates the specified exception message with the common information such as the current protocol,
         * authentication scheme, proxy address, and destination address.
         */
        protected string ExceptionMessage(string msg)
        {
            if (msg == null)
            {
                msg = "";
            }

            StringBuilder buf = new StringBuilder(128 + msg.Length)
                .Append(this.Protocol)
                .Append(", ")
                .Append(this.AuthScheme)
                .Append(", ")
                .Append(this.proxyAddress)
                .Append(" => ")
                .Append(this.destinationAddress);

            if (!string.IsNullOrEmpty(msg))
            {
                buf.Append(", ").Append(msg);
            }

            return buf.ToString();
        }

        public override void ChannelReadComplete(IChannelHandlerContext ctx)
        {
            if (this.suppressChannelReadComplete)
            {
                this.suppressChannelReadComplete = false;

                ReadIfNeeded(ctx);
            }
            else
            {
                ctx.FireChannelReadComplete();
            }
        }

        public override ValueTask WriteAsync(IChannelHandlerContext context, object message)
        {
            if (this.finished)
            {
                this.WritePendingWrites();
                return this.ctx.WriteAsync(message);
            }
            else
            {
                return this.AddPendingWrite(this.ctx, message);
            }
        }

        public override void Flush(IChannelHandlerContext context)
        {
            if (this.finished)
            {
                this.WritePendingWrites();
                this.ctx.Flush();
            }
            else
            {
                this.flushedPrematurely = true;
            }
        }

        static void ReadIfNeeded(IChannelHandlerContext ctx)
        {
            if (!ctx.Channel.Configuration.AutoRead)
            {
                ctx.Read();
            }
        }

        void WritePendingWrites()
        {
            if (this.pendingWrites != null)
            {
                this.pendingWrites.RemoveAndWriteAllAsync();
                this.pendingWrites = null;
            }
        }

        void FailPendingWrites(Exception cause)
        {
            if (this.pendingWrites != null)
            {
                this.pendingWrites.RemoveAndFailAll(cause);
                this.pendingWrites = null;
            }
        }

        ValueTask AddPendingWrite(IChannelHandlerContext ctx, object msg)
        {
            PendingWriteQueue pendingWrites = this.pendingWrites;
            if (pendingWrites == null)
            {
                this.pendingWrites = pendingWrites = new PendingWriteQueue(ctx);
            }

            return pendingWrites.Add(msg);
        }

        protected IEventExecutor Executor
        {
            get
            {
                if (this.ctx == null)
                {
                    throw new Exception("Should not reach here");
                }

                return this.ctx.Executor;
            }
        }
    }
}