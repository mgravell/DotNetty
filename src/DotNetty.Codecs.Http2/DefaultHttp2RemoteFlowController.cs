// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Common;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;

    /**
 * Basic implementation of {@link Http2RemoteFlowController}.
 * <p>
 * This class is <strong>NOT</strong> thread safe. The assumption is all methods must be invoked from a single thread.
 * Typically this thread is the event loop thread for the {@link IChannelHandlerContext} managed by this class.
 */
    public class DefaultHttp2RemoteFlowController : Http2ConnectionAdapter, Http2RemoteFlowController
    {
        static readonly IInternalLogger logger = InternalLoggerFactory.GetInstance<DefaultHttp2RemoteFlowController>();
        const int MIN_WRITABLE_CHUNK = 32 * 1024;
        readonly Http2Connection connection;
        readonly Http2ConnectionPropertyKey stateKey;
        readonly StreamByteDistributor streamByteDistributor;
        readonly FlowState connectionState;
        int _initialWindowSize = Http2CodecUtil.DEFAULT_WINDOW_SIZE;
        WritabilityMonitor monitor;
        IChannelHandlerContext ctx;

        public DefaultHttp2RemoteFlowController(Http2Connection connection)
            : this(connection, (Http2FlowControlListener)null)
        {
        }

        public DefaultHttp2RemoteFlowController(Http2Connection connection, StreamByteDistributor streamByteDistributor)
            : this(connection, streamByteDistributor, null)
        {
        }

        public DefaultHttp2RemoteFlowController(Http2Connection connection, Http2FlowControlListener listener)
            : this(connection, new WeightedFairQueueByteDistributor(connection), listener)
        {
        }

        public DefaultHttp2RemoteFlowController(Http2Connection connection, StreamByteDistributor streamByteDistributor, Http2FlowControlListener listener)
        {
            Contract.Requires(connection != null);
            Contract.Requires(streamByteDistributor != null);
            this.connection = connection;
            this.streamByteDistributor = streamByteDistributor;

            // Add a flow state for the connection.
            this.stateKey = connection.newKey();
            this.connectionState = new FlowState(this, this.connection.connectionStream());
            connection.connectionStream().setProperty(this.stateKey, this.connectionState);

            // Monitor may depend upon connectionState, and so initialize after connectionState
            this.listener(listener);
            this.monitor.windowSize(this.connectionState, this._initialWindowSize);

            // Register for notification of new streams.
            connection.addListener(this);
        }

        public override void onStreamAdded(Http2Stream stream)
        {
            // If the stream state is not open then the stream is not yet eligible for flow controlled frames and
            // only requires the ReducedFlowState. Otherwise the full amount of memory is required.
            stream.setProperty(this.stateKey, new FlowState(this, stream));
        }

        public override void onStreamActive(Http2Stream stream)
        {
            // If the object was previously created, but later activated then we have to ensure the proper
            // _initialWindowSize is used.
            this.monitor.windowSize(this.state(stream), this._initialWindowSize);
        }

        public override void onStreamClosed(Http2Stream stream)
        {
            // Any pending frames can never be written, cancel and
            // write errors for any pending frames.
            this.state(stream).cancel();
        }

        public override void onStreamHalfClosed(Http2Stream stream)
        {
            if (Http2StreamState.HALF_CLOSED_LOCAL.Equals(stream.state()))
            {
                /**
                 * When this method is called there should not be any
                 * pending frames left if the API is used correctly. However,
                 * it is possible that a erroneous application can sneak
                 * in a frame even after having already written a frame with the
                 * END_STREAM flag set, as the stream state might not transition
                 * immediately to HALF_CLOSED_LOCAL / CLOSED due to flow control
                 * delaying the write.
                 *
                 * This is to cancel any such illegal writes.
                 */
                this.state(stream).cancel();
            }
        }

        /**
     * {@inheritDoc}
     * <p>
     * Any queued {@link FlowControlled} objects will be sent.
     */
        public void channelHandlerContext(IChannelHandlerContext ctx)
        {
            Contract.Requires(ctx != null);
            this.ctx = ctx;

            // Writing the pending bytes will not check writability change and instead a writability change notification
            // to be provided by an explicit call.
            this.channelWritabilityChanged();

            // Don't worry about cleaning up queued frames here if ctx is null. It is expected that all streams will be
            // closed and the queue cleanup will occur when the stream state transitions occur.

            // If any frames have been queued up, we should send them now that we have a channel context.
            if (this.isChannelWritable())
            {
                this.writePendingBytes();
            }
        }

        public IChannelHandlerContext channelHandlerContext()
        {
            return this.ctx;
        }

        public void initialWindowSize(int newWindowSize)
        {
            Contract.Assert(this.ctx == null || this.ctx.Executor.InEventLoop);
            this.monitor.initialWindowSize(newWindowSize);
        }

        public int initialWindowSize()
        {
            return this._initialWindowSize;
        }

        public int windowSize(Http2Stream stream)
        {
            return this.state(stream).windowSize();
        }

        public bool isWritable(Http2Stream stream)
        {
            return this.monitor.isWritable(this.state(stream));
        }

        public void channelWritabilityChanged()
        {
            this.monitor.channelWritabilityChange();
        }

        public void updateDependencyTree(int childStreamId, int parentStreamId, short weight, bool exclusive)
        {
            // It is assumed there are all validated at a higher level. For example in the Http2FrameReader.
            Contract.Assert(weight >= Http2CodecUtil.MIN_WEIGHT && weight <= Http2CodecUtil.MAX_WEIGHT, "Invalid weight");
            Contract.Assert(childStreamId != parentStreamId, "A stream cannot depend on itself");
            Contract.Assert(childStreamId > 0 && parentStreamId >= 0, "childStreamId must be > 0. parentStreamId must be >= 0.");

            this.streamByteDistributor.updateDependencyTree(childStreamId, parentStreamId, weight, exclusive);
        }

        bool isChannelWritable()
        {
            return this.ctx != null && this.isChannelWritable0();
        }

        bool isChannelWritable0()
        {
            return this.ctx.Channel.IsWritable;
        }

        public void listener(Http2FlowControlListener listener)
        {
            this.monitor = listener == null ? new WritabilityMonitor(this) : new ListenerWritabilityMonitor(this, listener);
        }

        public void incrementWindowSize(Http2Stream stream, int delta)
        {
            Contract.Assert(this.ctx == null || this.ctx.Executor.InEventLoop);
            this.monitor.incrementWindowSize(this.state(stream), delta);
        }

        public void addFlowControlled(Http2Stream stream, FlowControlled frame)
        {
            // The context can be null assuming the frame will be queued and send later when the context is set.
            Contract.Assert(this.ctx == null || this.ctx.Executor.InEventLoop);
            Contract.Requires(frame != null);
            try
            {
                this.monitor.enqueueFrame(this.state(stream), frame);
            }
            catch (Exception t)
            {
                frame.error(this.ctx, t);
            }
        }

        public bool hasFlowControlled(Http2Stream stream)
        {
            return this.state(stream).hasFrame();
        }

        FlowState state(Http2Stream stream)
        {
            return stream.getProperty<FlowState>(this.stateKey);
        }

        /**
     * Returns the flow control window for the entire connection.
     */
        int connectionWindowSize()
        {
            return this.connectionState.windowSize();
        }

        int minUsableChannelBytes()
        {
            // The current allocation algorithm values "fairness" and doesn't give any consideration to "goodput". It
            // is possible that 1 byte will be allocated to many streams. In an effort to try to make "goodput"
            // reasonable with the current allocation algorithm we have this "cheap" check up front to ensure there is
            // an "adequate" amount of connection window before allocation is attempted. This is not foolproof as if the
            // number of streams is >= this minimal number then we may still have the issue, but the idea is to narrow the
            // circumstances in which this can happen without rewriting the allocation algorithm.
            return Math.Max(this.ctx.Channel.Configuration.WriteBufferLowWaterMark, MIN_WRITABLE_CHUNK);
        }

        int maxUsableChannelBytes()
        {
            // If the channel isWritable, allow at least minUsableChannelBytes.
            int channelWritableBytes = (int)Math.Min(int.MaxValue, this.ctx.Channel.BytesBeforeUnwritable);
            int usableBytes = channelWritableBytes > 0 ? Math.Max(channelWritableBytes, this.minUsableChannelBytes()) : 0;

            // Clip the usable bytes by the connection window.
            return Math.Min(this.connectionState.windowSize(), usableBytes);
        }

        /**
     * The amount of bytes that can be supported by underlying {@link io.netty.channel.Channel} without
     * queuing "too-much".
     */
        int writableBytes()
        {
            return Math.Min(this.connectionWindowSize(), this.maxUsableChannelBytes());
        }

        public void writePendingBytes()
        {
            this.monitor.writePendingBytes();
        }

        /**
     * The remote flow control state for a single stream.
     */
        sealed class FlowState : StreamByteDistributorContext
        {
            readonly DefaultHttp2RemoteFlowController controller;
            readonly Http2Stream _stream;
            readonly IDequeue<FlowControlled> pendingWriteQueue;
            int window;
            long _pendingBytes;
            bool markedWritable;

            /**
         * Set to true while a frame is being written, false otherwise.
         */
            bool writing;

            /**
         * Set to true if cancel() was called.
         */
            bool cancelled;

            internal FlowState(DefaultHttp2RemoteFlowController controller, Http2Stream stream)
            {
                this.controller = controller;
                this._stream = stream;
                this.pendingWriteQueue = new Deque<FlowControlled>(2);
            }

            /**
         * Determine if the stream associated with this object is writable.
         * @return {@code true} if the stream associated with this object is writable.
         */
            internal bool isWritable()
            {
                return this.windowSize() > this.pendingBytes() && !this.cancelled;
            }

            /**
         * The stream this state is associated with.
         */
            public Http2Stream stream()
            {
                return this._stream;
            }

            /**
         * Returns the parameter from the last call to {@link #markedWritability(bool)}.
         */
            internal bool markedWritability()
            {
                return this.markedWritable;
            }

            /**
         * Save the state of writability.
         */
            internal void markedWritability(bool isWritable)
            {
                this.markedWritable = isWritable;
            }

            public int windowSize()
            {
                return this.window;
            }

            /**
         * Reset the window size for this stream.
         */
            internal void windowSize(int initialWindowSize)
            {
                this.window = initialWindowSize;
            }

            /**
         * Write the allocated bytes for this stream.
         * @return the number of bytes written for a stream or {@code -1} if no write occurred.
         */
            internal int writeAllocatedBytes(int allocated)
            {
                int initialAllocated = allocated;
                int writtenBytes;
                // In case an exception is thrown we want to remember it and pass it to cancel(Exception).
                Exception cause = null;
                FlowControlled frame;
                try
                {
                    Contract.Assert(!this.writing);
                    this.writing = true;

                    // Write the remainder of frames that we are allowed to
                    bool writeOccurred = false;
                    while (!this.cancelled && (frame = this.peek()) != null)
                    {
                        int maxBytes = Math.Min(allocated, this.writableWindow());
                        if (maxBytes <= 0 && frame.size() > 0)
                        {
                            // The frame still has data, but the amount of allocated bytes has been exhausted.
                            // Don't write needless empty frames.
                            break;
                        }

                        writeOccurred = true;
                        int initialFrameSize = frame.size();
                        try
                        {
                            frame.write(this.controller.ctx, Math.Max(0, maxBytes));
                            if (frame.size() == 0)
                            {
                                // This frame has been fully written, remove this frame and notify it.
                                // Since we remove this frame first, we're guaranteed that its error
                                // method will not be called when we call cancel.
                                this.pendingWriteQueue.TryDequeue(out var _);//.remove();
                                frame.writeComplete();
                            }
                        }
                        finally
                        {
                            // Decrement allocated by how much was actually written.
                            allocated -= initialFrameSize - frame.size();
                        }
                    }

                    if (!writeOccurred)
                    {
                        // Either there was no frame, or the amount of allocated bytes has been exhausted.
                        return -1;
                    }
                }
                catch (Exception t)
                {
                    // Mark the state as cancelled, we'll clear the pending queue via cancel() below.
                    this.cancelled = true;
                    cause = t;
                }
                finally
                {
                    this.writing = false;
                    // Make sure we always decrement the flow control windows
                    // by the bytes written.
                    writtenBytes = initialAllocated - allocated;

                    this.decrementPendingBytes(writtenBytes, false);
                    this.decrementFlowControlWindow(writtenBytes);

                    // If a cancellation occurred while writing, call cancel again to
                    // clear and error all of the pending writes.
                    if (this.cancelled)
                    {
                        this.cancel(cause);
                    }
                }

                return writtenBytes;
            }

            /**
         * Increments the flow control window for this stream by the given delta and returns the new value.
         */
            internal int incrementStreamWindow(int delta)
            {
                if (delta > 0 && int.MaxValue - delta < this.window)
                {
                    throw Http2Exception.streamError(this._stream.id(), Http2Error.FLOW_CONTROL_ERROR, "Window size overflow for stream: {0}", this._stream.id());
                }

                this.window += delta;

                this.controller.streamByteDistributor.updateStreamableBytes(this);
                return this.window;
            }

            /**
         * Returns the maximum writable window (minimum of the stream and connection windows).
         */
            int writableWindow()
            {
                return Math.Min(this.window, this.controller.connectionWindowSize());
            }

            public long pendingBytes()
            {
                return this._pendingBytes;
            }

            /**
         * Adds the {@code frame} to the pending queue and increments the pending byte count.
         */
            internal void enqueueFrame(FlowControlled frame)
            {
                if (!this.pendingWriteQueue.TryPeekLast(out FlowControlled last))
                {
                    this.enqueueFrameWithoutMerge(frame);
                    return;
                }

                int lastSize = last.size();
                if (last.merge(this.controller.ctx, frame))
                {
                    this.incrementPendingBytes(last.size() - lastSize, true);
                    return;
                }

                this.enqueueFrameWithoutMerge(frame);
            }

            void enqueueFrameWithoutMerge(FlowControlled frame)
            {
                this.pendingWriteQueue.TryEnqueue(frame);
                // This must be called after adding to the queue in order so that hasFrame() is
                // updated before updating the stream state.
                this.incrementPendingBytes(frame.size(), true);
            }

            public bool hasFrame()
            {
                return !this.pendingWriteQueue.IsEmpty;
            }

            /**
         * Returns the the head of the pending queue, or {@code null} if empty.
         */
            FlowControlled peek()
            {
                return this.pendingWriteQueue.TryPeek(out FlowControlled result) ? result : null;
            }

            /**
         * Any operations that may be pending are cleared and the status of these operations is failed.
         */
            internal void cancel()
            {
                this.cancel(null);
            }

            /**
         * Clears the pending queue and writes errors for each remaining frame.
         * @param cause the {@link Exception} that caused this method to be invoked.
         */
            void cancel(Exception cause)
            {
                this.cancelled = true;
                // Ensure that the queue can't be modified while we are writing.
                if (this.writing)
                {
                    return;
                }

                Http2Exception exception = null;
                while (this.pendingWriteQueue.TryDequeue(out FlowControlled frame))
                {
                    exception = exception ?? Http2Exception.streamError(this._stream.id(), Http2Error.INTERNAL_ERROR, cause, "Stream closed before write could take place");
                    this.writeError(frame, exception);
                }

                this.controller.streamByteDistributor.updateStreamableBytes(this);

                this.controller.monitor.stateCancelled(this);
            }

            /**
         * Increments the number of pending bytes for this node and optionally updates the
         * {@link StreamByteDistributor}.
         */
            void incrementPendingBytes(int numBytes, bool updateStreamableBytes)
            {
                this._pendingBytes += numBytes;
                this.controller.monitor.incrementPendingBytes(numBytes);
                if (updateStreamableBytes)
                {
                    this.controller.streamByteDistributor.updateStreamableBytes(this);
                }
            }

            /**
         * If this frame is in the pending queue, decrements the number of pending bytes for the stream.
         */
            void decrementPendingBytes(int bytes, bool updateStreamableBytes)
            {
                this.incrementPendingBytes(-bytes, updateStreamableBytes);
            }

            /**
         * Decrement the per stream and connection flow control window by {@code bytes}.
         */
            void decrementFlowControlWindow(int bytes)
            {
                try
                {
                    int negativeBytes = -bytes;
                    this.controller.connectionState.incrementStreamWindow(negativeBytes);
                    this.incrementStreamWindow(negativeBytes);
                }
                catch (Http2Exception e)
                {
                    // Should never get here since we're decrementing.
                    throw new InvalidOperationException("Invalid window state when writing frame: " + e.Message, e);
                }
            }

            /**
         * Discards this {@link FlowControlled}, writing an error. If this frame is in the pending queue,
         * the unwritten bytes are removed from this branch of the priority tree.
         */
            void writeError(FlowControlled frame, Http2Exception cause)
            {
                IChannelHandlerContext ctx = this.controller.ctx;
                Contract.Assert(ctx != null);
                this.decrementPendingBytes(frame.size(), true);
                frame.error(ctx, cause);
            }
        }

        /**
     * Abstract class which provides common functionality for writability monitor implementations.
     */
        class WritabilityMonitor
        {
            protected readonly DefaultHttp2RemoteFlowController controller;

            bool inWritePendingBytes;
            long totalPendingBytes;

            internal WritabilityMonitor(DefaultHttp2RemoteFlowController controller)
            {
                this.controller = controller;
            }

            void Write(Http2Stream stream, int numBytes) => this.controller.state(stream).writeAllocatedBytes(numBytes);

            /**
             * Called when the writability of the underlying channel changes.
             * @ If a write occurs and an exception happens in the write operation.
             */
            protected internal virtual void channelWritabilityChange()
            {
            }

            /**
             * Called when the state is cancelled.
             * @param state the state that was cancelled.
             */
            protected internal virtual void stateCancelled(FlowState state)
            {
            }

            /**
             * Set the initial window size for {@code state}.
             * @param state the state to change the initial window size for.
             * @param initialWindowSize the size of the window in bytes.
             */
            protected internal virtual void windowSize(FlowState state, int initialWindowSize)
            {
                state.windowSize(initialWindowSize);
            }

            /**
             * Increment the window size for a particular stream.
             * @param state the state associated with the stream whose window is being incremented.
             * @param delta The amount to increment by.
             * @ If this operation overflows the window for {@code state}.
             */
            protected internal virtual void incrementWindowSize(FlowState state, int delta)
            {
                state.incrementStreamWindow(delta);
            }

            /**
             * Add a frame to be sent via flow control.
             * @param state The state associated with the stream which the {@code frame} is associated with.
             * @param frame the frame to enqueue.
             * @ If a writability error occurs.
             */
            protected internal virtual void enqueueFrame(FlowState state, FlowControlled frame)
            {
                state.enqueueFrame(frame);
            }

            /**
             * Increment the total amount of pending bytes for all streams. When any stream's pending bytes changes
             * method should be called.
             * @param delta The amount to increment by.
             */
            internal void incrementPendingBytes(int delta)
            {
                this.totalPendingBytes += delta;

                // Notification of writibilty change should be delayed until the end of the top level event.
                // This is to ensure the flow controller is more consistent state before calling external listener methods.
            }

            /**
             * Determine if the stream associated with {@code state} is writable.
             * @param state The state which is associated with the stream to test writability for.
             * @return {@code true} if {@link FlowState#stream()} is writable. {@code false} otherwise.
             */
            protected internal bool isWritable(FlowState state)
            {
                return this.isWritableConnection() && state.isWritable();
            }

            internal void writePendingBytes()
            {
                // Reentry is not permitted during the byte distribution process. It may lead to undesirable distribution of
                // bytes and even infinite loops. We protect against reentry and make sure each call has an opportunity to
                // cause a distribution to occur. This may be useful for example if the channel's writability changes from
                // Writable -> Not Writable (because we are writing) -> Writable (because the user flushed to make more room
                // in the channel outbound buffer).
                if (this.inWritePendingBytes)
                {
                    return;
                }

                this.inWritePendingBytes = true;
                try
                {
                    int bytesToWrite = this.controller.writableBytes();
                    // Make sure we always write at least once, regardless if we have bytesToWrite or not.
                    // This ensures that zero-length frames will always be written.
                    for (;;)
                    {
                        if (!this.controller.streamByteDistributor.distribute(bytesToWrite, this.Write) ||
                            (bytesToWrite = this.controller.writableBytes()) <= 0 ||
                            !this.controller.isChannelWritable0())
                        {
                            break;
                        }
                    }
                }
                finally
                {
                    this.inWritePendingBytes = false;
                }
            }

            protected internal virtual void initialWindowSize(int newWindowSize)
            {
                if (newWindowSize < 0)
                {
                    throw new ArgumentException("Invalid initial window size: " + newWindowSize);
                }

                int delta = newWindowSize - this.controller._initialWindowSize;
                this.controller._initialWindowSize = newWindowSize;
                this.controller.connection.forEachActiveStream(Visit);

                if (delta > 0 && this.controller.isChannelWritable())
                {
                    // The window size increased, send any pending frames for all streams.
                    this.writePendingBytes();
                }

                bool Visit(Http2Stream stream)
                {
                    this.controller.state(stream).incrementStreamWindow(delta);
                    return true;
                }
            }

            protected bool isWritableConnection()
            {
                return this.controller.connectionState.windowSize() - this.totalPendingBytes > 0 && this.controller.isChannelWritable();
            }
        }

        /**
         * Writability of a {@code stream} is calculated using the following:
         * <pre>
         * Connection Window - Total Queued Bytes > 0 &&
         * Stream Window - Bytes Queued for Stream > 0 &&
         * isChannelWritable()
         * </pre>
         */
        class ListenerWritabilityMonitor : WritabilityMonitor
        {
            readonly Http2FlowControlListener listener;

            bool checkStreamWritabilityVisitor(Http2Stream stream)
            {
                FlowState state = this.controller.state(stream);
                if (this.isWritable(state) != state.markedWritability())
                {
                    this.notifyWritabilityChanged(state);
                }

                return true;
            }

            internal ListenerWritabilityMonitor(DefaultHttp2RemoteFlowController controller, Http2FlowControlListener listener)
                : base(controller)
            {
                this.listener = listener;
            }

            protected internal override void windowSize(FlowState state, int initialWindowSize)
            {
                base.windowSize(state, initialWindowSize);
                try
                {
                    this.checkStateWritability(state);
                }
                catch (Http2Exception e)
                {
                    throw new Exception("Caught unexpected exception from window", e);
                }
            }

            protected internal override void incrementWindowSize(FlowState state, int delta)
            {
                base.incrementWindowSize(state, delta);
                this.checkStateWritability(state);
            }

            protected internal override void initialWindowSize(int newWindowSize)
            {
                base.initialWindowSize(newWindowSize);
                if (this.isWritableConnection())
                {
                    // If the write operation does not occur we still need to check all streams because they
                    // may have transitioned from writable to not writable.
                    this.checkAllWritabilityChanged();
                }
            }

            protected internal override void enqueueFrame(FlowState state, FlowControlled frame)
            {
                base.enqueueFrame(state, frame);
                this.checkConnectionThenStreamWritabilityChanged(state);
            }

            protected internal override void stateCancelled(FlowState state)
            {
                try
                {
                    this.checkConnectionThenStreamWritabilityChanged(state);
                }
                catch (Http2Exception e)
                {
                    throw new Exception("Caught unexpected exception from checkAllWritabilityChanged", e);
                }
            }

            protected internal override void channelWritabilityChange()
            {
                if (this.controller.connectionState.markedWritability() != this.controller.isChannelWritable())
                {
                    this.checkAllWritabilityChanged();
                }
            }

            void checkStateWritability(FlowState state)
            {
                if (this.isWritable(state) != state.markedWritability())
                {
                    if (state == this.controller.connectionState)
                    {
                        this.checkAllWritabilityChanged();
                    }
                    else
                    {
                        this.notifyWritabilityChanged(state);
                    }
                }
            }

            void notifyWritabilityChanged(FlowState state)
            {
                state.markedWritability(!state.markedWritability());
                try
                {
                    this.listener.writabilityChanged(state.stream());
                }
                catch (Exception cause)
                {
                    logger.Error("Caught Exception from listener.writabilityChanged", cause);
                }
            }

            void checkConnectionThenStreamWritabilityChanged(FlowState state)
            {
                // It is possible that the connection window and/or the individual stream writability could change.
                if (this.isWritableConnection() != this.controller.connectionState.markedWritability())
                {
                    this.checkAllWritabilityChanged();
                }
                else if (this.isWritable(state) != state.markedWritability())
                {
                    this.notifyWritabilityChanged(state);
                }
            }

            void checkAllWritabilityChanged()
            {
                // Make sure we mark that we have notified as a result of this change.
                this.controller.connectionState.markedWritability(this.isWritableConnection());
                this.controller.connection.forEachActiveStream(this.checkStreamWritabilityVisitor);
            }
        }
    }
}