// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
    * A single stream within an HTTP2 connection. Streams are compared to each other by priority.
    */
    public interface Http2Stream
    {
        /**
         * Gets the unique identifier for this stream within the connection.
         */
        int id();

        /**
         * Gets the state of this stream.
         */
        Http2StreamState state();

        /**
         * Opens this stream, making it available via {@link Http2Connection#forEachActiveStream(Http2StreamVisitor)} and
         * transition state to:
         * <ul>
         * <li>{@link State#OPEN} if {@link #state()} is {@link State#IDLE} and {@code halfClosed} is {@code false}.</li>
         * <li>{@link State#HALF_CLOSED_LOCAL} if {@link #state()} is {@link State#IDLE} and {@code halfClosed}
         * is {@code true} and the stream is local.</li>
         * <li>{@link State#HALF_CLOSED_REMOTE} if {@link #state()} is {@link State#IDLE} and {@code halfClosed}
         * is {@code true} and the stream is remote.</li>
         * <li>{@link State#RESERVED_LOCAL} if {@link #state()} is {@link State#HALF_CLOSED_REMOTE}.</li>
         * <li>{@link State#RESERVED_REMOTE} if {@link #state()} is {@link State#HALF_CLOSED_LOCAL}.</li>
         * </ul>
         */
        Http2Stream open(bool halfClosed);

        /**
         * Closes the stream.
         */
        Http2Stream close();

        /**
         * Closes the local side of this stream. If this makes the stream closed, the child is closed as
         * well.
         */
        Http2Stream closeLocalSide();

        /**
         * Closes the remote side of this stream. If this makes the stream closed, the child is closed
         * as well.
         */
        Http2Stream closeRemoteSide();

        /**
         * Indicates whether a {@code RST_STREAM} frame has been sent from the local endpoint for this stream.
         */
        bool isResetSent();

        /**
         * Sets the flag indicating that a {@code RST_STREAM} frame has been sent from the local endpoint
         * for this stream. This does not affect the stream state.
         */
        Http2Stream resetSent();

        /**
         * Associates the application-defined data with this stream.
         * @return The value that was previously associated with {@code key}, or {@code null} if there was none.
         */
        T setProperty<T>(Http2ConnectionPropertyKey key, T value) where T : class;

        /**
         * Returns application-defined data if any was associated with this stream.
         */
        T getProperty<T>(Http2ConnectionPropertyKey key) where T : class;

        /**
         * Returns and removes application-defined data if any was associated with this stream.
         */
        T removeProperty<T>(Http2ConnectionPropertyKey key) where T : class;

        /**
         * Indicates that headers have been sent to the remote endpoint on this stream. The first call to this method would
         * be for the initial headers (see {@link #isHeadersSent()}} and the second call would indicate the trailers
         * (see {@link #isTrailersReceived()}).
         * @param isInformational {@code true} if the headers contain an informational status code (for responses only).
         */
        Http2Stream headersSent(bool isInformational);

        /**
         * Indicates whether or not headers were sent to the remote endpoint.
         */
        bool isHeadersSent();

        /**
         * Indicates whether or not trailers were sent to the remote endpoint.
         */
        bool isTrailersSent();

        /**
         * Indicates that headers have been received. The first call to this method would be for the initial headers
         * (see {@link #isHeadersReceived()}} and the second call would indicate the trailers
         * (see {@link #isTrailersReceived()}).
         * @param isInformational {@code true} if the headers contain an informational status code (for responses only).
         */
        Http2Stream headersReceived(bool isInformational);

        /**
         * Indicates whether or not the initial headers have been received.
         */
        bool isHeadersReceived();

        /**
         * Indicates whether or not the trailers have been received.
         */
        bool isTrailersReceived();

        /**
         * Indicates that a push promise was sent to the remote endpoint.
         */
        Http2Stream pushPromiseSent();

        /**
         * Indicates whether or not a push promise was sent to the remote endpoint.
         */
        bool isPushPromiseSent();
    }
}