// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Transport.Channels;

    public interface Http2FlowController
    {
        /**
         * Set the {@link ChannelHandlerContext} for which to apply flow control on.
         * <p>
         * This <strong>must</strong> be called to properly initialize the {@link Http2FlowController}.
         * Not calling this is considered a programming error.
         * @param ctx The {@link ChannelHandlerContext} for which to apply flow control on.
         * @throws Http2Exception if any protocol-related error occurred.
         */
        void channelHandlerContext(IChannelHandlerContext ctx);

        /**
         * Sets the connection-wide initial flow control window and updates all stream windows (but not the connection
         * stream window) by the delta.
         * <p>
         * Represents the value for
         * <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_INITIAL_WINDOW_SIZE</a>. This method should
         * only be called by Netty (not users) as a result of a receiving a {@code SETTINGS} frame.
         *
         * @param newWindowSize the new initial window size.
         * @throws Http2Exception thrown if any protocol-related error occurred.
         */
        void initialWindowSize(int newWindowSize);

        /**
         * Gets the connection-wide initial flow control window size that is used as the basis for new stream flow
         * control windows.
         * <p>
         * Represents the value for
         * <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_INITIAL_WINDOW_SIZE</a>. The initial value
         * returned by this method must be {@link Http2CodecUtil#DEFAULT_WINDOW_SIZE}.
         */
        int initialWindowSize();

        /**
         * Get the portion of the flow control window for the given stream that is currently available for sending/receiving
         * frames which are subject to flow control. This quantity is measured in number of bytes.
         */
        int windowSize(Http2Stream stream);

        /**
         * Increments the size of the stream's flow control window by the given delta.
         * <p>
         * In the case of a {@link Http2RemoteFlowController} this is called upon receipt of a
         * {@code WINDOW_UPDATE} frame from the remote endpoint to mirror the changes to the window
         * size.
         * <p>
         * For a {@link Http2LocalFlowController} this can be called to request the expansion of the
         * window size published by this endpoint. It is up to the implementation, however, as to when a
         * {@code WINDOW_UPDATE} is actually sent.
         *
         * @param stream The subject stream. Use {@link Http2Connection#connectionStream()} for
         *            requesting the size of the connection window.
         * @param delta the change in size of the flow control window.
         * @throws Http2Exception thrown if a protocol-related error occurred.
         */
        void incrementWindowSize(Http2Stream stream, int delta);
    }
}