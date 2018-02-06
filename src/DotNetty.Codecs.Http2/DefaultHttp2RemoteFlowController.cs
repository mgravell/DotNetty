namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Transport.Channels;

    /**
 * Basic implementation of {@link Http2RemoteFlowController}.
 * <p>
 * This class is <strong>NOT</strong> thread safe. The assumption is all methods must be invoked from a single thread.
 * Typically this thread is the event loop thread for the {@link IChannelHandlerContext} managed by this class.
 */
    public class DefaultHttp2RemoteFlowController : Http2RemoteFlowController {
        static readonly IInternalLogger logger = InternalLoggerFactory.GetInstance<DefaultHttp2RemoteFlowController>();
        const int MIN_WRITABLE_CHUNK = 32 * 1024;
        readonly Http2Connection connection;
        readonly Http2ConnectionPropertyKey stateKey;
        readonly StreamByteDistributor streamByteDistributor;
        readonly FlowState connectionState;
        private int _initialWindowSize = Http2CodecUtil.DEFAULT_WINDOW_SIZE;
        private WritabilityMonitor monitor;
        private IChannelHandlerContext ctx;

        public DefaultHttp2RemoteFlowController(Http2Connection connection) :this(connection, (Http2FlowControlListener) null)
        {
        }

        public DefaultHttp2RemoteFlowController(Http2Connection connection, StreamByteDistributor streamByteDistributor) :this(connection, streamByteDistributor, null)
        {
        }

        public DefaultHttp2RemoteFlowController(Http2Connection connection, Http2FlowControlListener listener) : this(connection, new WeightedFairQueueByteDistributor(connection), listener);
        {
        }

        public DefaultHttp2RemoteFlowController(Http2Connection connection,
            StreamByteDistributor streamByteDistributor,
            final Http2FlowControlListener listener) {
            this.connection = checkNotNull(connection, "connection");
            this.streamByteDistributor = checkNotNull(streamByteDistributor, "streamWriteDistributor");

            // Add a flow state for the connection.
            this.stateKey = connection.newKey();
            this.connectionState = new FlowState(connection.connectionStream());
            connection.connectionStream().setProperty(this.stateKey, this.connectionState);

            // Monitor may depend upon connectionState, and so initialize after connectionState
            listener(listener);
            this.monitor.windowSize(this.connectionState, _initialWindowSize);

            // Register for notification of new streams.
            connection.addListener(new Http2ConnectionAdapter() {
                @Override
                public void onStreamAdded(Http2Stream stream) {
                // If the stream state is not open then the stream is not yet eligible for flow controlled frames and
                // only requires the ReducedFlowState. Otherwise the full amount of memory is required.
                stream.setProperty(stateKey, new FlowState(stream));
            }

            @Override
            public void onStreamActive(Http2Stream stream) {
                // If the object was previously created, but later activated then we have to ensure the proper
                // _initialWindowSize is used.
                this.monitor.windowSize(this.state(stream), _initialWindowSize);
            }

            @Override
            public void onStreamClosed(Http2Stream stream) {
                // Any pending frames can never be written, cancel and
                // write errors for any pending frames.
                this.state(stream).cancel();
            }

            @Override
            public void onStreamHalfClosed(Http2Stream stream) {
                if (HALF_CLOSED_LOCAL.equals(stream.state())) {
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
            });
        }

        /**
     * {@inheritDoc}
     * <p>
     * Any queued {@link FlowControlled} objects will be sent.
     */
        @Override
        public void IChannelHandlerContext(IChannelHandlerContext ctx)  {
            this.ctx = checkNotNull(ctx, "ctx");

            // Writing the pending bytes will not check writability change and instead a writability change notification
            // to be provided by an explicit call.
            this.channelWritabilityChanged();

            // Don't worry about cleaning up queued frames here if ctx is null. It is expected that all streams will be
            // closed and the queue cleanup will occur when the stream state transitions occur.

            // If any frames have been queued up, we should send them now that we have a channel context.
            if (this.isChannelWritable()) {
                writePendingBytes();
            }
        }

        @Override
        public IChannelHandlerContext IChannelHandlerContext() {
            return this.ctx;
        }

        @Override
        public void initialWindowSize(int newWindowSize)  {
            assert this.ctx == null || this.ctx.Executor.InEventLoop;
            this.monitor.initialWindowSize(newWindowSize);
        }

        @Override
        public int initialWindowSize() {
            return _initialWindowSize;
        }

        @Override
        public int windowSize(Http2Stream stream) {
            return this.state(stream).windowSize();
        }

        @Override
        public bool isWritable(Http2Stream stream) {
            return this.monitor.isWritable(this.state(stream));
        }

        @Override
        public void channelWritabilityChanged()  {
            this.monitor.channelWritabilityChange();
        }

        @Override
        public void updateDependencyTree(int childStreamId, int parentStreamId, short weight, bool exclusive) {
            // It is assumed there are all validated at a higher level. For example in the Http2FrameReader.
            assert weight >= MIN_WEIGHT && weight <= MAX_WEIGHT : "Invalid weight";
            assert childStreamId != parentStreamId : "A stream cannot depend on itself";
            assert childStreamId > 0 && parentStreamId >= 0 : "childStreamId must be > 0. parentStreamId must be >= 0.";

            this.streamByteDistributor.updateDependencyTree(childStreamId, parentStreamId, weight, exclusive);
        }

        private bool isChannelWritable() {
            return this.ctx != null && this.isChannelWritable0();
        }

        private bool isChannelWritable0() {
            return this.ctx.channel().isWritable();
        }

        @Override
        public void listener(Http2FlowControlListener listener) {
            this.monitor = listener == null ? new WritabilityMonitor() : new ListenerWritabilityMonitor(listener);
        }

        @Override
        public void incrementWindowSize(Http2Stream stream, int delta)  {
            assert this.ctx == null || this.ctx.Executor.InEventLoop;
            this.monitor.incrementWindowSize(this.state(stream), delta);
        }

        @Override
        public void addFlowControlled(Http2Stream stream, FlowControlled frame) {
            // The context can be null assuming the frame will be queued and send later when the context is set.
            assert this.ctx == null || this.ctx.Executor.InEventLoop;
            checkNotNull(frame, "frame");
            try {
                this.monitor.enqueueFrame(this.state(stream), frame);
            } catch (Exception t) {
                frame.error(this.ctx, t);
            }
        }

        @Override
        public bool hasFlowControlled(Http2Stream stream) {
            return this.state(stream).hasFrame();
        }

        private FlowState state(Http2Stream stream) {
            return (FlowState) stream.getProperty(this.stateKey);
        }

        /**
     * Returns the flow control window for the entire connection.
     */
        private int connectionWindowSize() {
            return this.connectionState.windowSize();
        }

        private int minUsableChannelBytes() {
            // The current allocation algorithm values "fairness" and doesn't give any consideration to "goodput". It
            // is possible that 1 byte will be allocated to many streams. In an effort to try to make "goodput"
            // reasonable with the current allocation algorithm we have this "cheap" check up front to ensure there is
            // an "adequate" amount of connection window before allocation is attempted. This is not foolproof as if the
            // number of streams is >= this minimal number then we may still have the issue, but the idea is to narrow the
            // circumstances in which this can happen without rewriting the allocation algorithm.
            return max(this.ctx.channel().config().getWriteBufferLowWaterMark(), this.MIN_WRITABLE_CHUNK);
        }

        private int maxUsableChannelBytes() {
            // If the channel isWritable, allow at least minUsableChannelBytes.
            int channelWritableBytes = (int) min(Integer.MAX_VALUE, this.ctx.channel().bytesBeforeUnwritable());
            int usableBytes = channelWritableBytes > 0 ? max(channelWritableBytes, this.minUsableChannelBytes()) : 0;

            // Clip the usable bytes by the connection window.
            return min(this.connectionState.windowSize(), usableBytes);
        }

        /**
     * The amount of bytes that can be supported by underlying {@link io.netty.channel.Channel} without
     * queuing "too-much".
     */
        private int writableBytes() {
            return min(this.connectionWindowSize(), this.maxUsableChannelBytes());
        }

        @Override
        public void writePendingBytes()  {
            this.monitor.writePendingBytes();
        }

        /**
     * The remote flow control state for a single stream.
     */
        readonly class FlowState : StreamByteDistributor.StreamState {
            readonly Http2Stream stream;
            readonly Deque<FlowControlled> pendingWriteQueue;
            private int window;
            private long pendingBytes;
            private bool markedWritable;

            /**
         * Set to true while a frame is being written, false otherwise.
         */
            private bool writing;
            /**
         * Set to true if cancel() was called.
         */
            private bool cancelled;

            FlowState(Http2Stream stream) {
                this.stream = stream;
                this.pendingWriteQueue = new ArrayDeque<FlowControlled>(2);
            }

            /**
         * Determine if the stream associated with this object is writable.
         * @return {@code true} if the stream associated with this object is writable.
         */
            bool isWritable() {
                return this.windowSize() > this.pendingBytes() && !this.cancelled;
            }

            /**
         * The stream this state is associated with.
         */
            @Override
            public Http2Stream stream() {
                return stream;
            }

            /**
         * Returns the parameter from the last call to {@link #markedWritability(bool)}.
         */
            bool markedWritability() {
                return this.markedWritable;
            }

            /**
         * Save the state of writability.
         */
            void markedWritability(bool isWritable) {
                this.markedWritable = isWritable;
            }

            @Override
            public int windowSize() {
                return this.window;
            }

            /**
         * Reset the window size for this stream.
         */
            void windowSize(int initialWindowSize) {
                this.window = initialWindowSize;
            }

            /**
         * Write the allocated bytes for this stream.
         * @return the number of bytes written for a stream or {@code -1} if no write occurred.
         */
            int writeAllocatedBytes(int allocated) {
                final int initialAllocated = allocated;
                int writtenBytes;
                // In case an exception is thrown we want to remember it and pass it to cancel(Exception).
                Exception cause = null;
                FlowControlled frame;
                try {
                    assert !this.writing;
                    this.writing = true;

                    // Write the remainder of frames that we are allowed to
                    bool writeOccurred = false;
                    while (!this.cancelled && (frame = this.peek()) != null) {
                        int maxBytes = min(allocated, this.writableWindow());
                        if (maxBytes <= 0 && frame.size() > 0) {
                            // The frame still has data, but the amount of allocated bytes has been exhausted.
                            // Don't write needless empty frames.
                            break;
                        }
                        writeOccurred = true;
                        int initialFrameSize = frame.size();
                        try {
                            frame.write(ctx, max(0, maxBytes));
                            if (frame.size() == 0) {
                                // This frame has been fully written, remove this frame and notify it.
                                // Since we remove this frame first, we're guaranteed that its error
                                // method will not be called when we call cancel.
                                this.pendingWriteQueue.remove();
                                frame.writeComplete();
                            }
                        } finally {
                            // Decrement allocated by how much was actually written.
                            allocated -= initialFrameSize - frame.size();
                        }
                    }

                    if (!writeOccurred) {
                        // Either there was no frame, or the amount of allocated bytes has been exhausted.
                        return -1;
                    }

                } catch (Exception t) {
                    // Mark the state as cancelled, we'll clear the pending queue via cancel() below.
                    this.cancelled = true;
                    cause = t;
                } finally {
                    this.writing = false;
                    // Make sure we always decrement the flow control windows
                    // by the bytes written.
                    writtenBytes = initialAllocated - allocated;

                    decrementPendingBytes(writtenBytes, false);
                    this.decrementFlowControlWindow(writtenBytes);

                    // If a cancellation occurred while writing, call cancel again to
                    // clear and error all of the pending writes.
                    if (this.cancelled) {
                        this.cancel(cause);
                    }
                }
                return writtenBytes;
            }

            /**
         * Increments the flow control window for this stream by the given delta and returns the new value.
         */
            int incrementStreamWindow(int delta)  {
                if (delta > 0 && Integer.MAX_VALUE - delta < this.window) {
                    throw streamError(stream.id(), FLOW_CONTROL_ERROR,
                        "Window size overflow for stream: %d", stream.id());
                }
                this.window += delta;

                streamByteDistributor.updateStreamableBytes(this);
                return this.window;
            }

            /**
         * Returns the maximum writable window (minimum of the stream and connection windows).
         */
            private int writableWindow() {
                return min(this.window, connectionWindowSize());
            }

            @Override
            public long pendingBytes() {
                return pendingBytes;
            }

            /**
         * Adds the {@code frame} to the pending queue and increments the pending byte count.
         */
            void enqueueFrame(FlowControlled frame) {
                FlowControlled last = this.pendingWriteQueue.peekLast();
                if (last == null) {
                    this.enqueueFrameWithoutMerge(frame);
                    return;
                }

                int lastSize = last.size();
                if (last.merge(ctx, frame)) {
                    incrementPendingBytes(last.size() - lastSize, true);
                    return;
                }
                this.enqueueFrameWithoutMerge(frame);
            }

            private void enqueueFrameWithoutMerge(FlowControlled frame) {
                this.pendingWriteQueue.offer(frame);
                // This must be called after adding to the queue in order so that hasFrame() is
                // updated before updating the stream state.
                incrementPendingBytes(frame.size(), true);
            }

            @Override
            public bool hasFrame() {
                return !this.pendingWriteQueue.isEmpty();
            }

            /**
         * Returns the the head of the pending queue, or {@code null} if empty.
         */
            private FlowControlled peek() {
                return this.pendingWriteQueue.peek();
            }

            /**
         * Any operations that may be pending are cleared and the status of these operations is failed.
         */
            void cancel() {
                cancel(null);
            }

            /**
         * Clears the pending queue and writes errors for each remaining frame.
         * @param cause the {@link Exception} that caused this method to be invoked.
         */
            private void cancel(Exception cause) {
                this.cancelled = true;
                // Ensure that the queue can't be modified while we are writing.
                if (this.writing) {
                    return;
                }

                FlowControlled frame = this.pendingWriteQueue.poll();
                if (frame != null) {
                    // Only create exception once and reuse to reduce overhead of filling in the stacktrace.
                    final Http2Exception exception = streamError(stream.id(), INTERNAL_ERROR, cause,
                        "Stream closed before write could take place");
                    do {
                        this.writeError(frame, exception);
                        frame = this.pendingWriteQueue.poll();
                    } while (frame != null);
                }

                streamByteDistributor.updateStreamableBytes(this);

                monitor.stateCancelled(this);
            }

            /**
         * Increments the number of pending bytes for this node and optionally updates the
         * {@link StreamByteDistributor}.
         */
            private void incrementPendingBytes(int numBytes, bool updateStreamableBytes) {
                pendingBytes += numBytes;
                monitor.incrementPendingBytes(numBytes);
                if (updateStreamableBytes) {
                    streamByteDistributor.updateStreamableBytes(this);
                }
            }

            /**
         * If this frame is in the pending queue, decrements the number of pending bytes for the stream.
         */
            private void decrementPendingBytes(int bytes, bool updateStreamableBytes) {
                this.incrementPendingBytes(-bytes, updateStreamableBytes);
            }

            /**
         * Decrement the per stream and connection flow control window by {@code bytes}.
         */
            private void decrementFlowControlWindow(int bytes) {
                try {
                    int negativeBytes = -bytes;
                    connectionState.incrementStreamWindow(negativeBytes);
                    this.incrementStreamWindow(negativeBytes);
                } catch (Http2Exception e) {
                    // Should never get here since we're decrementing.
                    throw new IllegalStateException("Invalid window state when writing frame: " + e.getMessage(), e);
                }
            }

            /**
         * Discards this {@link FlowControlled}, writing an error. If this frame is in the pending queue,
         * the unwritten bytes are removed from this branch of the priority tree.
         */
            private void writeError(FlowControlled frame, Http2Exception cause) {
                assert ctx != null;
                decrementPendingBytes(frame.size(), true);
                frame.error(ctx, cause);
            }
        }

        /**
     * Abstract class which provides common functionality for writability monitor implementations.
     */
        private class WritabilityMonitor {
            private bool inWritePendingBytes;
            private long totalPendingBytes;
            readonly Writer writer = new StreamByteDistributor.Writer() {
                @Override
                public void write(Http2Stream stream, int numBytes) {
                state(stream).writeAllocatedBytes(numBytes);
            }
        };

        /**
         * Called when the writability of the underlying channel changes.
         * @ If a write occurs and an exception happens in the write operation.
         */
        void channelWritabilityChange()  { }

        /**
         * Called when the state is cancelled.
         * @param state the state that was cancelled.
         */
        void stateCancelled(FlowState state) { }

        /**
         * Set the initial window size for {@code state}.
         * @param state the state to change the initial window size for.
         * @param initialWindowSize the size of the window in bytes.
         */
        void windowSize(FlowState state, int initialWindowSize) {
            state.windowSize(initialWindowSize);
        }

        /**
         * Increment the window size for a particular stream.
         * @param state the state associated with the stream whose window is being incremented.
         * @param delta The amount to increment by.
         * @ If this operation overflows the window for {@code state}.
         */
        void incrementWindowSize(FlowState state, int delta)  {
            state.incrementStreamWindow(delta);
        }

        /**
         * Add a frame to be sent via flow control.
         * @param state The state associated with the stream which the {@code frame} is associated with.
         * @param frame the frame to enqueue.
         * @ If a writability error occurs.
         */
        void enqueueFrame(FlowState state, FlowControlled frame)  {
            state.enqueueFrame(frame);
        }

        /**
         * Increment the total amount of pending bytes for all streams. When any stream's pending bytes changes
         * method should be called.
         * @param delta The amount to increment by.
         */
        final void incrementPendingBytes(int delta) {
            totalPendingBytes += delta;

            // Notification of writibilty change should be delayed until the end of the top level event.
            // This is to ensure the flow controller is more consistent state before calling external listener methods.
        }

        /**
         * Determine if the stream associated with {@code state} is writable.
         * @param state The state which is associated with the stream to test writability for.
         * @return {@code true} if {@link FlowState#stream()} is writable. {@code false} otherwise.
         */
        final bool isWritable(FlowState state) {
            return isWritableConnection() && state.isWritable();
        }

        final void writePendingBytes()  {
            // Reentry is not permitted during the byte distribution process. It may lead to undesirable distribution of
            // bytes and even infinite loops. We protect against reentry and make sure each call has an opportunity to
            // cause a distribution to occur. This may be useful for example if the channel's writability changes from
            // Writable -> Not Writable (because we are writing) -> Writable (because the user flushed to make more room
            // in the channel outbound buffer).
            if (inWritePendingBytes) {
                return;
            }
            inWritePendingBytes = true;
            try {
                int bytesToWrite = this.writableBytes();
                // Make sure we always write at least once, regardless if we have bytesToWrite or not.
                // This ensures that zero-length frames will always be written.
                for (;;) {
                    if (!this.streamByteDistributor.distribute(bytesToWrite, writer) ||
                        (bytesToWrite = this.writableBytes()) <= 0 ||
                        !this.isChannelWritable0()) {
                        break;
                    }
                }
            } finally {
                inWritePendingBytes = false;
            }
        }

        void initialWindowSize(int newWindowSize)  {
            if (newWindowSize < 0) {
                throw new IllegalArgumentException("Invalid initial window size: " + newWindowSize);
            }

            final int delta = newWindowSize - initialWindowSize;
            initialWindowSize = newWindowSize;
            this.connection.forEachActiveStream(new Http2StreamVisitor() {
                @Override
                public bool visit(Http2Stream stream)  {
                state(stream).incrementStreamWindow(delta);
                return true;
            }
            });

            if (delta > 0 && this.isChannelWritable()) {
                // The window size increased, send any pending frames for all streams.
                writePendingBytes();
            }
        }

        final bool isWritableConnection() {
            return this.connectionState.windowSize() - totalPendingBytes > 0 && this.isChannelWritable();
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
    readonly class ListenerWritabilityMonitor extends WritabilityMonitor {
    readonly Http2FlowControlListener listener;
    readonly Http2StreamVisitor checkStreamWritabilityVisitor = new Http2StreamVisitor() {
            @Override
            public bool visit(Http2Stream stream)  {
            FlowState state = state(stream);
            if (isWritable(state) != state.markedWritability()) {
            notifyWritabilityChanged(state);
        }
        return true;
    }
    };

    ListenerWritabilityMonitor(Http2FlowControlListener listener) {
    this.listener = listener;
    }

    @Override
    void windowSize(FlowState state, int initialWindowSize) {
    base.windowSize(state, initialWindowSize);
    try {
    checkStateWritability(state);
    } catch (Http2Exception e) {
    throw new RuntimeException("Caught unexpected exception from window", e);
    }
    }

    @Override
    void incrementWindowSize(FlowState state, int delta)  {
    base.incrementWindowSize(state, delta);
    checkStateWritability(state);
    }

    @Override
    void initialWindowSize(int newWindowSize)  {
    base.initialWindowSize(newWindowSize);
    if (isWritableConnection()) {
    // If the write operation does not occur we still need to check all streams because they
    // may have transitioned from writable to not writable.
    checkAllWritabilityChanged();
    }
    }

    @Override
    void enqueueFrame(FlowState state, FlowControlled frame)  {
    base.enqueueFrame(state, frame);
    checkConnectionThenStreamWritabilityChanged(state);
    }

    @Override
    void stateCancelled(FlowState state) {
    try {
    checkConnectionThenStreamWritabilityChanged(state);
    } catch (Http2Exception e) {
    throw new RuntimeException("Caught unexpected exception from checkAllWritabilityChanged", e);
    }
    }

    @Override
    void channelWritabilityChange()  {
    if (connectionState.markedWritability() != isChannelWritable()) {
    checkAllWritabilityChanged();
    }
    }

    private void checkStateWritability(FlowState state)  {
    if (isWritable(state) != state.markedWritability()) {
    if (state == connectionState) {
    checkAllWritabilityChanged();
    } else {
    notifyWritabilityChanged(state);
    }
    }
    }

    private void notifyWritabilityChanged(FlowState state) {
    state.markedWritability(!state.markedWritability());
    try {
    listener.writabilityChanged(state.stream);
    } catch (Exception cause) {
    logger.error("Caught Exception from listener.writabilityChanged", cause);
    }
    }

    private void checkConnectionThenStreamWritabilityChanged(FlowState state)  {
    // It is possible that the connection window and/or the individual stream writability could change.
    if (isWritableConnection() != connectionState.markedWritability()) {
    checkAllWritabilityChanged();
    } else if (isWritable(state) != state.markedWritability()) {
    notifyWritabilityChanged(state);
    }
    }

    private void checkAllWritabilityChanged()  {
    // Make sure we mark that we have notified as a result of this change.
    connectionState.markedWritability(isWritableConnection());
    connection.forEachActiveStream(checkStreamWritabilityVisitor);
    }
    }
    }
}