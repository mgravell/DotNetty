// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
   using System;
   using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;

    /**
     * Manager for the state of an HTTP/2 connection with the remote end-point.
     */
    public interface Http2Connection
    {
        /**
           * Close this connection. No more new streams can be created after this point and
           * all streams that exists (active or otherwise) will be closed and removed.
           * <p>Note if iterating active streams via {@link #forEachActiveStream(Http2StreamVisitor)} and an exception is
           * thrown it is necessary to call this method again to ensure the close completes.
           * @param promise Will be completed when all streams have been removed, and listeners have been notified.
           * @return A future that will be completed when all streams have been removed, and listeners have been notified.
           */
        Task close(TaskCompletionSource promise);

        /**
           * Creates a new key that is unique within this {@link Http2Connection}.
           */
        Http2ConnectionPropertyKey newKey();

        /**
           * Adds a listener of stream life-cycle events.
           */
        void addListener(Http2ConnectionListener listener);

        /**
           * Removes a listener of stream life-cycle events. If the same listener was added multiple times
           * then only the first occurrence gets removed.
           */
        void removeListener(Http2ConnectionListener listener);

        /**
           * Gets the stream if it exists. If not, returns {@code null}.
           */
        Http2Stream stream(int streamId);

        /**
           * Indicates whether or not the given stream may have existed within this connection. This is a short form
           * for calling {@link Endpoint#mayHaveCreatedStream(int)} on both endpoints.
           */
        bool streamMayHaveExisted(int streamId);

        /**
           * Gets the stream object representing the connection, itself (i.e. stream zero). This object
           * always exists.
           */
        Http2Stream connectionStream();

        /**
           * Gets the number of streams that are actively in use (i.e. {@code OPEN} or {@code HALF CLOSED}).
           */
        int numActiveStreams();

        /**
           * Provide a means of iterating over the collection of active streams.
           *
           * @param visitor The visitor which will visit each active stream.
           * @return The stream before iteration stopped or {@code null} if iteration went past the end.
           */
        Http2Stream forEachActiveStream(Http2StreamVisitor visitor);
       
       Http2Stream forEachActiveStream(Func<Http2Stream, bool> visitor);

        /**
           * Indicates whether or not the local endpoint for this connection is the server.
           */
        bool isServer();

        /**
           * Gets a view of this connection from the local {@link Endpoint}.
           */
        Http2ConnectionEndpoint<Http2LocalFlowController> local();

        /**
           * Gets a view of this connection from the remote {@link Endpoint}.
           */
        Http2ConnectionEndpoint<Http2RemoteFlowController> remote();

        /**
           * Indicates whether or not a {@code GOAWAY} was received from the remote endpoint.
           */
        bool goAwayReceived();

        /**
           * Indicates that a {@code GOAWAY} was received from the remote endpoint and sets the last known stream.
           */
        void goAwayReceived(int lastKnownStream, long errorCode, IByteBuffer message);

        /**
           * Indicates whether or not a {@code GOAWAY} was sent to the remote endpoint.
           */
        bool goAwaySent();

        /**
           * Indicates that a {@code GOAWAY} was sent to the remote endpoint and sets the last known stream.
           */
        void goAwaySent(int lastKnownStream, long errorCode, IByteBuffer message);
    }
}