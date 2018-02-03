// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    /**
         * A view of the connection from one endpoint (local or remote).
         */
 public interface Http2ConnectionEndpoint<F>
        where F : Http2FlowController
    {
        /**
         * Increment and get the next generated stream id this endpoint. If negative, the stream IDs are
         * exhausted for this endpoint an no further streams may be created.
         */
        int incrementAndGetNextStreamId();

        /**
         * Indicates whether the given streamId is from the set of IDs used by this endpoint to
         * create new streams.
         */
        bool isValidStreamId(int streamId);

        /**
         * Indicates whether or not this endpoint may have created the given stream. This is {@code true} if
         * {@link #isValidStreamId(int)} and {@code streamId} <= {@link #lastStreamCreated()}.
         */
        bool mayHaveCreatedStream(int streamId);

        /**
         * Indicates whether or not this endpoint created the given stream.
         */
        bool created(Http2Stream stream);

        /**
         * Indicates whether or a stream created by this endpoint can be opened without violating
         * {@link #maxActiveStreams()}.
         */
        bool canOpenStream();

        /**
         * Creates a stream initiated by this endpoint. This could fail for the following reasons:
         * <ul>
         * <li>The requested stream ID is not the next sequential ID for this endpoint.</li>
         * <li>The stream already exists.</li>
         * <li>{@link #canOpenStream()} is {@code false}.</li>
         * <li>The connection is marked as going away.</li>
         * </ul>
         * <p>
         * The initial state of the stream will be immediately set before notifying {@link Listener}s. The state
         * transition is sensitive to {@code halfClosed} and is defined by {@link Http2Stream#open(bool)}.
         * @param streamId The ID of the stream
         * @param halfClosed see {@link Http2Stream#open(bool)}.
         * @see Http2Stream#open(bool)
         */
        Http2Stream createStream(int streamId, bool halfClosed);

        /**
         * Creates a push stream in the reserved state for this endpoint and notifies all listeners.
         * This could fail for the following reasons:
         * <ul>
         * <li>Server push is not allowed to the opposite endpoint.</li>
         * <li>The requested stream ID is not the next sequential stream ID for this endpoint.</li>
         * <li>The number of concurrent streams is above the allowed threshold for this endpoint.</li>
         * <li>The connection is marked as going away.</li>
         * <li>The parent stream ID does not exist or is not {@code OPEN} from the side sending the push
         * promise.</li>
         * <li>Could not set a valid priority for the new stream.</li>
         * </ul>
         *
         * @param streamId the ID of the push stream
         * @param parent the parent stream used to initiate the push stream.
         */
        Http2Stream reservePushStream(int streamId, Http2Stream parent);

        /**
         * Indicates whether or not this endpoint is the server-side of the connection.
         */
        bool isServer();

        /**
         * This is the <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_ENABLE_PUSH</a> value sent
         * from the opposite endpoint. This method should only be called by Netty (not users) as a result of a
         * receiving a {@code SETTINGS} frame.
         */
        void allowPushTo(bool allow);

        /**
         * This is the <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_ENABLE_PUSH</a> value sent
         * from the opposite endpoint. The initial value must be {@code true} for the client endpoint and always false
         * for a server endpoint.
         */
        bool allowPushTo();

        /**
         * Gets the number of active streams (i.e. {@code OPEN} or {@code HALF CLOSED}) that were created by this
         * endpoint.
         */
        int numActiveStreams();

        /**
         * Gets the maximum number of streams (created by this endpoint) that are allowed to be active at
         * the same time. This is the
         * <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_CONCURRENT_STREAMS</a>
         * value sent from the opposite endpoint to restrict stream creation by this endpoint.
         * <p>
         * The default value returned by this method must be "unlimited".
         */
        int maxActiveStreams();

        /**
         * Sets the limit for {@code SETTINGS_MAX_CONCURRENT_STREAMS}.
         * @param maxActiveStreams The maximum number of streams (created by this endpoint) that are allowed to be
         * active at once. This is the
         * <a href="https://tools.ietf.org/html/rfc7540#section-6.5.2">SETTINGS_MAX_CONCURRENT_STREAMS</a> value sent
         * from the opposite endpoint to restrict stream creation by this endpoint.
         */
        void maxActiveStreams(int maxActiveStreams);

        /**
         * Gets the ID of the stream last successfully created by this endpoint.
         */
        int lastStreamCreated();

        /**
         * If a GOAWAY was received for this endpoint, this will be the last stream ID from the
         * GOAWAY frame. Otherwise, this will be {@code -1}.
         */
        int lastStreamKnownByPeer();

        /**
         * Gets the flow controller for this endpoint.
         */
        F flowController();

        /**
         * Sets the flow controller for this endpoint.
         */
        void flowController(F flowController);

        /**
         * Gets the {@link Endpoint} opposite this one.
         */
        Http2ConnectionEndpoint<Http2FlowController> opposite();
    }
}