// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Transport.Channels;

    /**
     * Basic implementation of {@link Http2LocalFlowController}.
     * <p>
     * This class is <strong>NOT</strong> thread safe. The assumption is all methods must be invoked from a single thread.
     * Typically this thread is the event loop thread for the {@link IChannelHandlerContext} managed by this class.
     */
    public class DefaultHttp2LocalFlowController : Http2LocalFlowController
    {
        /**
         * The default ratio of window size to initial window size below which a {@code WINDOW_UPDATE}
         * is sent to expand the window.
         */
        public static readonly float DEFAULT_WINDOW_UPDATE_RATIO = 0.5f;

        readonly Http2Connection connection;
        readonly Http2ConnectionPropertyKey stateKey;
        Http2FrameWriter _frameWriter;
        IChannelHandlerContext ctx;
        float _windowUpdateRatio;
        int _initialWindowSize = Http2CodecUtil.DEFAULT_WINDOW_SIZE;

        public DefaultHttp2LocalFlowController(Http2Connection connection)
            : this(connection, DEFAULT_WINDOW_UPDATE_RATIO, false)
        {
        }

        /**
     * Constructs a controller with the given settings.
     *
     * @param connection the connection state.
     * @param windowUpdateRatio the window percentage below which to send a {@code WINDOW_UPDATE}.
     * @param autoRefillConnectionWindow if {@code true}, effectively disables the connection window
     * in the flow control algorithm as they will always refill automatically without requiring the
     * application to consume the bytes. When enabled, the maximum bytes you must be prepared to
     * queue is proportional to {@code maximum number of concurrent streams * the initial window
     * size per stream}
     * (<a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_CONCURRENT_STREAMS</a>
     * <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_INITIAL_WINDOW_SIZE</a>).
     */
        public DefaultHttp2LocalFlowController(Http2Connection connection, float windowUpdateRatio, bool autoRefillConnectionWindow)
        {
            Contract.Requires(connection != null);
            this.connection = connection;
            this.windowUpdateRatio(windowUpdateRatio);

            // Add a flow state for the connection.
            this.stateKey = connection.newKey();
            FlowState connectionState = autoRefillConnectionWindow
                ? new AutoRefillState(this, connection.connectionStream(), this._initialWindowSize)
                : new DefaultState(this, connection.connectionStream(), this._initialWindowSize);
            connection.connectionStream().setProperty(this.stateKey, connectionState);

            // Register for notification of new streams.
            connection.addListener(new StateTracker(this));
        }

        class StateTracker : Http2ConnectionAdapter
        {
            readonly DefaultHttp2LocalFlowController controller;

            public StateTracker(DefaultHttp2LocalFlowController controller)
            {
                this.controller = controller;
            }

            public override void onStreamAdded(Http2Stream stream)
            {
                // Unconditionally used the reduced flow control state because it requires no object allocation
                // and the DefaultFlowState will be allocated in onStreamActive.
                stream.setProperty(this.controller.stateKey, REDUCED_FLOW_STATE);
            }

            public override void onStreamActive(Http2Stream stream)
            {
                // Need to be sure the stream's initial window is adjusted for SETTINGS
                // frames which may have been exchanged while it was in IDLE
                stream.setProperty(this.controller.stateKey, new DefaultState(this.controller, stream, this.controller._initialWindowSize));
            }

            public override void onStreamClosed(Http2Stream stream)
            {
                try
                {
                    // When a stream is closed, consume any remaining bytes so that they
                    // are restored to the connection window.
                    FlowState state = this.controller.state(stream);
                    int unconsumedBytes = state.unconsumedBytes();
                    if (this.controller.ctx != null && unconsumedBytes > 0)
                    {
                        this.controller.connectionState().consumeBytes(unconsumedBytes);
                        state.consumeBytes(unconsumedBytes);
                    }
                }
                /*catch (Http2Exception e)
                {
                    
                    PlatformDependent.throwException(e);
                }*/
                finally
                {
                    // Unconditionally reduce the amount of memory required for flow control because there is no
                    // object allocation costs associated with doing so and the stream will not have any more
                    // local flow control state to keep track of anymore.
                    stream.setProperty(this.controller.stateKey, REDUCED_FLOW_STATE);
                }
            }
        }

        public Http2LocalFlowController frameWriter(Http2FrameWriter frameWriter)
        {
            Contract.Requires(frameWriter != null);
            this._frameWriter = frameWriter;
            return this;
        }

        public void channelHandlerContext(IChannelHandlerContext ctx)
        {
            Contract.Requires(ctx != null);
            this.ctx = ctx;
        }

        public void initialWindowSize(int newWindowSize)
        {
            Contract.Assert(this.ctx == null || this.ctx.Executor.InEventLoop);
            int delta = newWindowSize - this._initialWindowSize;
            this._initialWindowSize = newWindowSize;

            WindowUpdateVisitor visitor = new WindowUpdateVisitor(this, delta);
            this.connection.forEachActiveStream(visitor);
            visitor.throwIfError();
        }

        public int initialWindowSize()
        {
            return this._initialWindowSize;
        }

        public int windowSize(Http2Stream stream)
        {
            return this.state(stream).windowSize();
        }

        public int initialWindowSize(Http2Stream stream)
        {
            return this.state(stream).initialWindowSize();
        }

        public void incrementWindowSize(Http2Stream stream, int delta)
        {
            Contract.Assert(this.ctx != null && this.ctx.Executor.InEventLoop);
            FlowState state = this.state(stream);
            // Just add the delta to the stream-specific initial window size so that the next time the window
            // expands it will grow to the new initial size.
            state.incrementInitialStreamWindow(delta);
            state.writeWindowUpdateIfNeeded();
        }

        public bool consumeBytes(Http2Stream stream, int numBytes)
        {
            Contract.Assert(this.ctx != null && this.ctx.Executor.InEventLoop);
            if (numBytes < 0)
            {
                throw new ArgumentException("numBytes must not be negative");
            }

            if (numBytes == 0)
            {
                return false;
            }

            // Streams automatically consume all remaining bytes when they are closed, so just ignore
            // if already closed.
            if (stream != null && !isClosed(stream))
            {
                if (stream.id() == Http2CodecUtil.CONNECTION_STREAM_ID)
                {
                    throw new NotSupportedException("Returning bytes for the connection window is not supported");
                }

                bool windowUpdateSent = this.connectionState().consumeBytes(numBytes);
                windowUpdateSent |= this.state(stream).consumeBytes(numBytes);
                return windowUpdateSent;
            }

            return false;
        }

        public int unconsumedBytes(Http2Stream stream)
        {
            return this.state(stream).unconsumedBytes();
        }

        static void checkValidRatio(float ratio)
        {
            if (ratio <= 0.0 || ratio >= 1.0)
            {
                throw new ArgumentException("Invalid ratio: " + ratio);
            }
        }

        /**
         * The window update ratio is used to determine when a window update must be sent. If the ratio
         * of bytes processed since the last update has meet or exceeded this ratio then a window update will
         * be sent. This is the global window update ratio that will be used for new streams.
         * @param ratio the ratio to use when checking if a {@code WINDOW_UPDATE} is determined necessary for new streams.
         * @throws ArgumentException If the ratio is out of bounds (0, 1).
         */
        public void windowUpdateRatio(float ratio)
        {
            Contract.Assert(this.ctx == null || this.ctx.Executor.InEventLoop);
            checkValidRatio(ratio);
            this._windowUpdateRatio = ratio;
        }

        /**
         * The window update ratio is used to determine when a window update must be sent. If the ratio
         * of bytes processed since the last update has meet or exceeded this ratio then a window update will
         * be sent. This is the global window update ratio that will be used for new streams.
         */
        public float windowUpdateRatio()
        {
            return this._windowUpdateRatio;
        }

        /**
         * The window update ratio is used to determine when a window update must be sent. If the ratio
         * of bytes processed since the last update has meet or exceeded this ratio then a window update will
         * be sent. This window update ratio will only be applied to {@code streamId}.
         * <p>
         * Note it is the responsibly of the caller to ensure that the the
         * initial {@code SETTINGS} frame is sent before this is called. It would
         * be considered a {@link Http2Error#PROTOCOL_ERROR} if a {@code WINDOW_UPDATE}
         * was generated by this method before the initial {@code SETTINGS} frame is sent.
         * @param stream the stream for which {@code ratio} applies to.
         * @param ratio the ratio to use when checking if a {@code WINDOW_UPDATE} is determined necessary.
         * @If a protocol-error occurs while generating {@code WINDOW_UPDATE} frames
         */
        public void windowUpdateRatio(Http2Stream stream, float ratio)
        {
            Contract.Assert(this.ctx != null && this.ctx.Executor.InEventLoop);
            checkValidRatio(ratio);
            FlowState state = this.state(stream);
            state.windowUpdateRatio(ratio);
            state.writeWindowUpdateIfNeeded();
        }

        /**
         * The window update ratio is used to determine when a window update must be sent. If the ratio
         * of bytes processed since the last update has meet or exceeded this ratio then a window update will
         * be sent. This window update ratio will only be applied to {@code streamId}.
         * @If no stream corresponding to {@code stream} could be found.
         */
        public float windowUpdateRatio(Http2Stream stream)
        {
            return this.state(stream).windowUpdateRatio();
        }

        public void receiveFlowControlledFrame(
            Http2Stream stream,
            IByteBuffer data,
            int padding,
            bool endOfStream)
        {
            Contract.Assert(this.ctx != null && this.ctx.Executor.InEventLoop);
            int dataLength = data.ReadableBytes + padding;

            // Apply the connection-level flow control
            FlowState connectionState = this.connectionState();
            connectionState.receiveFlowControlledFrame(dataLength);

            if (stream != null && !isClosed(stream))
            {
                // Apply the stream-level flow control
                FlowState state = this.state(stream);
                state.endOfStream(endOfStream);
                state.receiveFlowControlledFrame(dataLength);
            }
            else if (dataLength > 0)
            {
                // Immediately consume the bytes for the connection window.
                connectionState.consumeBytes(dataLength);
            }
        }

        FlowState connectionState()
        {
            return this.connection.connectionStream().getProperty<FlowState>(this.stateKey);
        }

        FlowState state(Http2Stream stream)
        {
            return stream.getProperty<FlowState>(this.stateKey);
        }

        static bool isClosed(Http2Stream stream)
        {
            return stream.state().Equals(Http2StreamState.CLOSED);
        }

        /**
         * Flow control state that does autorefill of the flow control window when the data is
         * received.
         */
        class AutoRefillState : DefaultState
        {
            public AutoRefillState(DefaultHttp2LocalFlowController controller, Http2Stream stream, int initialWindowSize)
                : base(controller, stream, initialWindowSize)
            {
            }

            public override void receiveFlowControlledFrame(int dataLength)
            {
                base.receiveFlowControlledFrame(dataLength);
                // Need to call the base to consume the bytes, since this.consumeBytes does nothing.
                base.consumeBytes(dataLength);
            }

            public override bool consumeBytes(int numBytes)
            {
                // Do nothing, since the bytes are already consumed upon receiving the data.
                return false;
            }
        }

        /**
         * Flow control window state for an individual stream.
         */
        class DefaultState : FlowState
        {
            readonly DefaultHttp2LocalFlowController controller;
            readonly Http2Stream stream;

            /**
             * The actual flow control window that is decremented as soon as {@code DATA} arrives.
             */
            int _window;

            /**
             * A view of {@link #window} that is used to determine when to send {@code WINDOW_UPDATE}
             * frames. Decrementing this window for received {@code DATA} frames is delayed until the
             * application has indicated that the data has been fully processed. This prevents sending
             * a {@code WINDOW_UPDATE} until the number of processed bytes drops below the threshold.
             */
            int processedWindow;

            /**
             * This is what is used to determine how many bytes need to be returned relative to {@link #processedWindow}.
             * Each stream has their own initial window size.
             */
            int initialStreamWindowSize;

            /**
             * This is used to determine when {@link #processedWindow} is sufficiently far away from
             * {@link #initialStreamWindowSize} such that a {@code WINDOW_UPDATE} should be sent.
             * Each stream has their own window update ratio.
             */
            float streamWindowUpdateRatio;

            int lowerBound;
            bool _endOfStream;

            public DefaultState(DefaultHttp2LocalFlowController controller, Http2Stream stream, int initialWindowSize)
            {
                this.controller = controller;
                this.stream = stream;
                this.window(initialWindowSize);
                this.streamWindowUpdateRatio = controller._windowUpdateRatio;
            }

            public void window(int initialWindowSize)
            {
                Contract.Assert(this.controller.ctx == null || this.controller.ctx.Executor.InEventLoop);
                this._window = this.processedWindow = this.initialStreamWindowSize = initialWindowSize;
            }

            public int windowSize()
            {
                return this._window;
            }

            public int initialWindowSize()
            {
                return this.initialStreamWindowSize;
            }

            public void endOfStream(bool endOfStream)
            {
                this._endOfStream = endOfStream;
            }

            public float windowUpdateRatio()
            {
                return this.streamWindowUpdateRatio;
            }

            public void windowUpdateRatio(float ratio)
            {
                Contract.Assert(this.controller.ctx == null || this.controller.ctx.Executor.InEventLoop);
                this.streamWindowUpdateRatio = ratio;
            }

            public void incrementInitialStreamWindow(int delta)
            {
                // Clip the delta so that the resulting initialStreamWindowSize falls within the allowed range.
                int newValue = (int)Math.Min(
                    Http2CodecUtil.MAX_INITIAL_WINDOW_SIZE,
                    Math.Max(Http2CodecUtil.MIN_INITIAL_WINDOW_SIZE, this.initialStreamWindowSize + (long)delta));
                delta = newValue - this.initialStreamWindowSize;

                this.initialStreamWindowSize += delta;
            }

            public void incrementFlowControlWindows(int delta)
            {
                if (delta > 0 && this._window > Http2CodecUtil.MAX_INITIAL_WINDOW_SIZE - delta)
                {
                    throw Http2Exception.streamError(
                        this.stream.id(),
                        Http2Error.FLOW_CONTROL_ERROR,
                        "Flow control window overflowed for stream: %d",
                        this.stream.id());
                }

                this._window += delta;
                this.processedWindow += delta;
                this.lowerBound = delta < 0 ? delta : 0;
            }

            public virtual void receiveFlowControlledFrame(int dataLength)
            {
                Contract.Assert(dataLength >= 0);

                // Apply the delta. Even if we throw an exception we want to have taken this delta into account.
                this._window -= dataLength;

                // Window size can become negative if we sent a SETTINGS frame that reduces the
                // size of the transfer window after the peer has written data frames.
                // The value is bounded by the length that SETTINGS frame decrease the window.
                // This difference is stored for the connection when writing the SETTINGS frame
                // and is cleared once we send a WINDOW_UPDATE frame.
                if (this._window < this.lowerBound)
                {
                    throw Http2Exception.streamError(
                        this.stream.id(),
                        Http2Error.FLOW_CONTROL_ERROR,
                        "Flow control window exceeded for stream: %d",
                        this.stream.id());
                }
            }

            void returnProcessedBytes(int delta)
            {
                if (this.processedWindow - delta < this._window)
                {
                    throw Http2Exception.streamError(
                        this.stream.id(),
                        Http2Error.INTERNAL_ERROR,
                        "Attempting to return too many bytes for stream %d",
                        this.stream.id());
                }

                this.processedWindow -= delta;
            }

            public virtual bool consumeBytes(int numBytes)
            {
                // Return the bytes processed and update the window.
                this.returnProcessedBytes(numBytes);
                return this.writeWindowUpdateIfNeeded();
            }

            public int unconsumedBytes()
            {
                return this.processedWindow - this._window;
            }

            public bool writeWindowUpdateIfNeeded()
            {
                if (this._endOfStream || this.initialStreamWindowSize <= 0)
                {
                    return false;
                }

                int threshold = (int)(this.initialStreamWindowSize * this.streamWindowUpdateRatio);
                if (this.processedWindow <= threshold)
                {
                    this.writeWindowUpdate();
                    return true;
                }

                return false;
            }

            /**
             * Called to perform a window update for this stream (or connection). Updates the window size back
             * to the size of the initial window and sends a window update frame to the remote endpoint.
             */
            void writeWindowUpdate()
            {
                // Expand the window for this stream back to the size of the initial window.
                int deltaWindowSize = this.initialStreamWindowSize - this.processedWindow;
                try
                {
                    this.incrementFlowControlWindows(deltaWindowSize);
                }
                catch (Exception t)
                {
                    throw Http2Exception.connectionError(
                        Http2Error.INTERNAL_ERROR,
                        t,
                        "Attempting to return too many bytes for stream %d",
                        this.stream.id());
                }

                // Send a window update for the stream/connection.
                this.controller._frameWriter.writeWindowUpdate(this.controller.ctx, this.stream.id(), deltaWindowSize, new TaskCompletionSource());
            }
        }

        /**
         * The local flow control state for a single stream that is not in a state where flow controlled frames cannot
         * be exchanged.
         */
        static readonly FlowState REDUCED_FLOW_STATE = new ReducedFlowState();

        /**
         * An abstraction which provides specific extensions used by local flow control.
         */
        interface FlowState
        {
            int windowSize();

            int initialWindowSize();

            void window(int initialWindowSize);

            /**
             * Increment the initial window size for this stream.
             * @param delta The amount to increase the initial window size by.
             */
            void incrementInitialStreamWindow(int delta);

            /**
             * Updates the flow control window for this stream if it is appropriate.
             *
             * @return true if {@code WINDOW_UPDATE} was written, false otherwise.
             */
            bool writeWindowUpdateIfNeeded();

            /**
             * Indicates that the application has consumed {@code numBytes} from the connection or stream and is
             * ready to receive more data.
             *
             * @param numBytes the number of bytes to be returned to the flow control window.
             * @return true if {@code WINDOW_UPDATE} was written, false otherwise.
             * @throws Http2Exception
             */
            bool consumeBytes(int numBytes);

            int unconsumedBytes();

            float windowUpdateRatio();

            void windowUpdateRatio(float ratio);

            /**
             * A flow control event has occurred and we should decrement the amount of available bytes for this stream.
             * @param dataLength The amount of data to for which this stream is no longer eligible to use for flow control.
             * @If too much data is used relative to how much is available.
             */
            void receiveFlowControlledFrame(int dataLength);

            /**
             * Increment the windows which are used to determine many bytes have been processed.
             * @param delta The amount to increment the window by.
             * @if integer overflow occurs on the window.
             */
            void incrementFlowControlWindows(int delta);

            void endOfStream(bool endOfStream);
        }

        class ReducedFlowState : FlowState
        {
            public int windowSize()
            {
                return 0;
            }

            public int initialWindowSize()
            {
                return 0;
            }

            public void window(int initialWindowSize)
            {
                throw new NotSupportedException();
            }

            public void incrementInitialStreamWindow(int delta)
            {
                // This operation needs to be supported during the initial settings exchange when
                // the peer has not yet acknowledged this peer being activated.
            }

            public bool writeWindowUpdateIfNeeded()
            {
                throw new NotSupportedException();
            }

            public bool consumeBytes(int numBytes)
            {
                return false;
            }

            public int unconsumedBytes()
            {
                return 0;
            }

            public float windowUpdateRatio()
            {
                throw new NotSupportedException();
            }

            public void windowUpdateRatio(float ratio)
            {
                throw new NotSupportedException();
            }

            public void receiveFlowControlledFrame(int dataLength)
            {
                throw new NotSupportedException();
            }

            public void incrementFlowControlWindows(int delta)
            {
                // This operation needs to be supported during the initial settings exchange when
                // the peer has not yet acknowledged this peer being activated.
            }

            public void endOfStream(bool endOfStream)
            {
                throw new NotSupportedException();
            }
        }

        /**
         * Provides a means to iterate over all active streams and increment the flow control windows.
         */
        class WindowUpdateVisitor : Http2StreamVisitor
        {
            CompositeStreamException compositeException;
            readonly DefaultHttp2LocalFlowController controller;
            readonly int delta;

            public WindowUpdateVisitor(DefaultHttp2LocalFlowController controller, int delta)
            {
                this.controller = controller;
                this.delta = delta;
            }

            public bool visit(Http2Stream stream)
            {
                try
                {
                    // Increment flow control window first so state will be consistent if overflow is detected.
                    FlowState state = this.controller.state(stream);
                    state.incrementFlowControlWindows(this.delta);
                    state.incrementInitialStreamWindow(this.delta);
                }
                catch (StreamException e)
                {
                    if (this.compositeException == null)
                    {
                        this.compositeException = new CompositeStreamException(e.error(), 4);
                    }

                    this.compositeException.add(e);
                }

                return true;
            }

            public void throwIfError()
            {
                if (this.compositeException != null)
                {
                    throw this.compositeException;
                }
            }
        }
    }
}