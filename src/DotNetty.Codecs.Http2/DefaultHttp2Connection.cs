/**
 * Simple implementation of {@link Http2Connection}.
 */

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Net.Security;
    using System.Threading.Tasks;
    using DotNetty.Buffers;
    using DotNetty.Common.Concurrency;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Internal.Logging;
    using DotNetty.Common.Utilities;

    public class DefaultHttp2Connection : Http2Connection
    {
        static readonly IInternalLogger logger = InternalLoggerFactory.GetInstance<DefaultHttp2Connection>();

        // Fields accessed by inner classes
        readonly Dictionary<int, DefaultStream> streamMap = new Dictionary<int, DefaultStream>();
        readonly PropertyKeyRegistry propertyKeyRegistry = new PropertyKeyRegistry();
        readonly ConnectionStream _connectionStream;
        readonly DefaultEndpoint<Http2LocalFlowController> localEndpoint;
        readonly DefaultEndpoint<Http2RemoteFlowController> remoteEndpoint;

        /**
     * We chose a {@link List} over a {@link Set} to avoid allocating an {@link IEnumerator} objects when iterating over
     * the listeners.
     * <p>
     * Initial size of 4 because the default configuration currently has 3 listeners
     * (local/remote flow controller and {@link StreamByteDistributor}) and we leave room for 1 extra.
     * We could be more aggressive but the ArrayList resize will double the size if we are too small.
     */
        readonly List<Http2ConnectionListener> listeners = new List<Http2ConnectionListener>(4);
        readonly ActiveStreams activeStreams;
        IPromise closePromise;

        /**
     * Creates a new connection with the given settings.
     * @param server whether or not this end-point is the server-side of the HTTP/2 connection.
     */
        public DefaultHttp2Connection(bool server)
            : this(server, Http2CodecUtil.DEFAULT_MAX_RESERVED_STREAMS)
        {
        }

        /**
     * Creates a new connection with the given settings.
     * @param server whether or not this end-point is the server-side of the HTTP/2 connection.
     * @param maxReservedStreams The maximum amount of streams which can exist in the reserved state for each endpoint.
     */
        public DefaultHttp2Connection(bool server, int maxReservedStreams)
        {
            this._connectionStream = new ConnectionStream(this);
            activeStreams = new ActiveStreams(this, listeners);
            // Reserved streams are excluded from the SETTINGS_MAX_CONCURRENT_STREAMS limit according to [1] and the RFC
            // doesn't define a way to communicate the limit on reserved streams. We rely upon the peer to send RST_STREAM
            // in response to any locally enforced limits being exceeded [2].
            // [1] https://tools.ietf.org/html/rfc7540#section-5.1.2
            // [2] https://tools.ietf.org/html/rfc7540#section-8.2.2
            localEndpoint = new DefaultEndpoint<Http2LocalFlowController>(this, server, server ? int.MaxValue : maxReservedStreams);
            remoteEndpoint = new DefaultEndpoint<Http2RemoteFlowController>(this, !server, maxReservedStreams);

            // Add the connection stream to the map.
            streamMap.Add(_connectionStream.id(), _connectionStream);
        }

        /**
     * Determine if {@link #close(Promise)} has been called and no more streams are allowed to be created.
     */
        bool isClosed()
        {
            return this.closePromise != null;
        }

        public Task close(IPromise promise)
        {
            Contract.Requires(promise != null);
            // Since we allow this method to be called multiple times, we must make sure that all the promises are notified
            // when all streams are removed and the close operation completes.
            if (closePromise != null)
            {
                if (closePromise == promise)
                {
                    // Do nothing
                }
                else if (promise.IsVoid)
                {
                    closePromise = promise;
                }
                else
                {
                    closePromise.Task.LinkOutcome(promise); //.addListener(new UnaryPromiseNotifier<Void>(promise));
                }
            }
            else
            {
                closePromise = promise;
            }

            if (isStreamMapEmpty())
            {
                promise.TryComplete();
                return promise.Task;
            }

            //IEnumerator<KeyValuePair<int, Http2Stream>> itr = streamMap.GetEnumerator();
            //copying streams to array to be able to modify streamMap
            DefaultStream[] streams = this.streamMap.Values.ToArray();

            // We must take care while iterating the streamMap as to not modify while iterating in case there are other code
            // paths iterating over the active streams.
            if (activeStreams.allowModifications())
            {
                activeStreams.incrementPendingIterations();
                try
                {
                    for (int i = 0; i < streams.Length; i++)
                    {
                        DefaultStream stream = streams[i];
                        if (stream.id() != Http2CodecUtil.CONNECTION_STREAM_ID)
                        {
                            // If modifications of the activeStream map is allowed, then a stream close operation will also
                            // modify the streamMap. Pass the iterator in so that remove will be called to prevent
                            // concurrent modification exceptions.
                            stream.close(true);
                        }
                    }
                }
                finally
                {
                    activeStreams.decrementPendingIterations();
                }
            }
            else
            {
                for (int i = 0; i < streams.Length; i++)
                {
                    DefaultStream stream = streams[i];
                    if (stream.id() != Http2CodecUtil.CONNECTION_STREAM_ID)
                    {
                        // We are not allowed to make modifications, so the close calls will be executed after this
                        // iteration completes.
                        stream.close();
                    }
                }
            }

            return closePromise.Task;
        }

        public void addListener(Http2ConnectionListener listener)
        {
            listeners.Add(listener);
        }

        public void removeListener(Http2ConnectionListener listener)
        {
            listeners.Remove(listener);
        }

        public bool isServer()
        {
            return localEndpoint.isServer();
        }

        public Http2Stream connectionStream()
        {
            return _connectionStream;
        }

        public Http2Stream stream(int streamId)
        {
            return streamMap.TryGetValue(streamId, out DefaultStream result) ? result : null;
        }

        public bool streamMayHaveExisted(int streamId)
        {
            return remoteEndpoint.mayHaveCreatedStream(streamId) || localEndpoint.mayHaveCreatedStream(streamId);
        }

        public int numActiveStreams()
        {
            return activeStreams.size();
        }

        public Http2Stream forEachActiveStream(Http2StreamVisitor visitor)
        {
            return activeStreams.forEachActiveStream(visitor.visit);
        }

        public Http2Stream forEachActiveStream(Func<Http2Stream, bool> visitor)
        {
            return activeStreams.forEachActiveStream(visitor);
        }

        public Http2ConnectionEndpoint<Http2LocalFlowController> local()
        {
            return localEndpoint;
        }

        public Http2ConnectionEndpoint<Http2RemoteFlowController> remote()
        {
            return remoteEndpoint;
        }

        public bool goAwayReceived()
        {
            return localEndpoint._lastStreamKnownByPeer >= 0;
        }

        public void goAwayReceived(int lastKnownStream, long errorCode, IByteBuffer debugData)
        {
            localEndpoint.lastStreamKnownByPeer(lastKnownStream);
            for (int i = 0; i < listeners.Count; ++i)
            {
                try
                {
                    listeners[i].onGoAwayReceived(lastKnownStream, errorCode, debugData);
                }
                catch (Exception cause)
                {
                    logger.Error("Caught Exception from listener onGoAwayReceived.", cause);
                }
            }

            forEachActiveStream(
                stream =>
                {
                    if (stream.id() > lastKnownStream && localEndpoint.isValidStreamId(stream.id()))
                    {
                        stream.close();
                    }

                    return true;
                }
            );
        }

        public bool goAwaySent()
        {
            return remoteEndpoint._lastStreamKnownByPeer >= 0;
        }

        public void goAwaySent(int lastKnownStream, long errorCode, IByteBuffer debugData)
        {
            remoteEndpoint.lastStreamKnownByPeer(lastKnownStream);
            for (int i = 0; i < listeners.Count; ++i)
            {
                try
                {
                    listeners[i].onGoAwaySent(lastKnownStream, errorCode, debugData);
                }
                catch (Exception cause)
                {
                    logger.Error("Caught Exception from listener onGoAwaySent.", cause);
                }
            }

            this.forEachActiveStream(
                stream =>
                {
                    if (stream.id() > lastKnownStream && remoteEndpoint.isValidStreamId(stream.id()))
                    {
                        stream.close();
                    }

                    return true;
                });
        }

        /**
         * Determine if {@link #streamMap} only contains the connection stream.
         */
        private bool isStreamMapEmpty()
        {
            return streamMap.Count == 1;
        }

        /**
         * Remove a stream from the {@link #streamMap}.
         * @param stream the stream to remove.
         * @param itr an iterator that may be pointing to the stream during iteration and {@link IEnumerator#remove()} will be
         * used if non-{@code null}.
         */
        void removeStream(DefaultStream stream)
        {
            bool removed = streamMap.Remove(stream.id()) != null;

            if (removed)
            {
                for (int i = 0; i < listeners.Count; i++)
                {
                    try
                    {
                        listeners[i].onStreamRemoved(stream);
                    }
                    catch (Exception cause)
                    {
                        logger.Error("Caught Exception from listener onStreamRemoved.", cause);
                    }
                }

                if (closePromise != null && isStreamMapEmpty())
                {
                    closePromise.TryComplete();
                }
            }
        }

        static Http2StreamState activeState(int streamId, Http2StreamState initialState, bool isLocal, bool halfClosed)
        {
            if (initialState == Http2StreamState.IDLE)
            {
                return halfClosed ? isLocal ? Http2StreamState.HALF_CLOSED_LOCAL : Http2StreamState.HALF_CLOSED_REMOTE : Http2StreamState.OPEN;
            }
            else if (initialState == Http2StreamState.RESERVED_LOCAL)
            {
                return Http2StreamState.HALF_CLOSED_REMOTE;
            }
            else if (initialState == Http2StreamState.RESERVED_REMOTE)
            {
                return Http2StreamState.HALF_CLOSED_LOCAL;
            }

            throw Http2Exception.streamError(streamId, Http2Error.PROTOCOL_ERROR, "Attempting to open a stream in an invalid state: " + initialState);
        }

        void notifyHalfClosed(Http2Stream stream)
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                try
                {
                    listeners[i].onStreamHalfClosed(stream);
                }
                catch (Exception cause)
                {
                    logger.Error("Caught Exception from listener onStreamHalfClosed.", cause);
                }
            }
        }

        void notifyClosed(Http2Stream stream)
        {
            for (int i = 0; i < listeners.Count; i++)
            {
                try
                {
                    listeners[i].onStreamClosed(stream);
                }
                catch (Exception cause)
                {
                    logger.Error("Caught Exception from listener onStreamClosed.", cause);
                }
            }
        }

        public Http2ConnectionPropertyKey newKey()
        {
            return propertyKeyRegistry.newKey(this);
        }

        /**
         * Verifies that the key is valid and returns it as the internal {@link DefaultPropertyKey} type.
         *
         * @throws NullPointerException if the key is {@code null}.
         * @throws ClassCastException if the key is not of type {@link DefaultPropertyKey}.
         * @throws ArgumentException if the key was not created by this connection.
         */
        DefaultPropertyKey verifyKey(Http2ConnectionPropertyKey key)
        {
            var dpk = key as DefaultPropertyKey;
            Contract.Requires(dpk != null);
            return dpk.verifyConnection(this);
        }

        /**
         * Simple stream implementation. Streams can be compared to each other by priority.
         */
        internal class DefaultStream : Http2Stream
        {
            static readonly byte META_STATE_SENT_RST = 1;
            static readonly byte META_STATE_SENT_HEADERS = 1 << 1;
            static readonly byte META_STATE_SENT_TRAILERS = 1 << 2;
            static readonly byte META_STATE_SENT_PUSHPROMISE = 1 << 3;
            static readonly byte META_STATE_RECV_HEADERS = 1 << 4;
            static readonly byte META_STATE_RECV_TRAILERS = 1 << 5;
            readonly DefaultHttp2Connection conn;
            readonly int _id;
            readonly PropertyMap properties;
            private Http2StreamState _state;
            private byte metaState;

            internal DefaultStream(DefaultHttp2Connection conn, int id, Http2StreamState state)
            {
                this.conn = conn;
                this._id = id;
                this._state = state;
                this.properties = new PropertyMap(conn.propertyKeyRegistry);
            }

            public int id()
            {
                return _id;
            }

            public Http2StreamState state()
            {
                return _state;
            }

            public virtual bool isResetSent()
            {
                return (metaState & META_STATE_SENT_RST) != 0;
            }

            public virtual Http2Stream resetSent()
            {
                metaState |= META_STATE_SENT_RST;
                return this;
            }

            public virtual Http2Stream headersSent(bool isInformational)
            {
                if (!isInformational)
                {
                    metaState |= isHeadersSent() ? META_STATE_SENT_TRAILERS : META_STATE_SENT_HEADERS;
                }

                return this;
            }

            public virtual bool isHeadersSent()
            {
                return (metaState & META_STATE_SENT_HEADERS) != 0;
            }

            public bool isTrailersSent()
            {
                return (metaState & META_STATE_SENT_TRAILERS) != 0;
            }

            public Http2Stream headersReceived(bool isInformational)
            {
                if (!isInformational)
                {
                    metaState |= isHeadersReceived() ? META_STATE_RECV_TRAILERS : META_STATE_RECV_HEADERS;
                }

                return this;
            }

            public bool isHeadersReceived()
            {
                return (metaState & META_STATE_RECV_HEADERS) != 0;
            }

            public bool isTrailersReceived()
            {
                return (metaState & META_STATE_RECV_TRAILERS) != 0;
            }

            public virtual Http2Stream pushPromiseSent()
            {
                metaState |= META_STATE_SENT_PUSHPROMISE;
                return this;
            }

            public virtual bool isPushPromiseSent()
            {
                return (metaState & META_STATE_SENT_PUSHPROMISE) != 0;
            }

            public V setProperty<V>(Http2ConnectionPropertyKey key, V value)
                where V : class
            {
                return properties.add(this.conn.verifyKey(key), value);
            }

            public V getProperty<V>(Http2ConnectionPropertyKey key)
                where V : class
            {
                return properties.get<V>(this.conn.verifyKey(key));
            }

            public V removeProperty<V>(Http2ConnectionPropertyKey key)
                where V : class
            {
                return properties.remove<V>(this.conn.verifyKey(key));
            }

            public virtual Http2Stream open(bool halfClosed)
            {
                _state = activeState(_id, _state, isLocal(), halfClosed);
                if (!createdBy().canOpenStream())
                {
                    throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "Maximum active streams violated for this endpoint.");
                }

                activate();
                return this;
            }

            internal void activate()
            {
                this.conn.activeStreams.activate(this);
            }

            public virtual Http2Stream close()
            {
                return close(false);
            }

            public virtual Http2Stream close(bool force)
            {
                if (_state == Http2StreamState.CLOSED)
                {
                    return this;
                }

                _state = Http2StreamState.CLOSED;

                --createdBy().numStreams;
                this.conn.activeStreams.deactivate(this, force);
                return this;
            }

            public virtual Http2Stream closeLocalSide()
            {
                if (this._state == Http2StreamState.OPEN)
                {
                    _state = Http2StreamState.HALF_CLOSED_LOCAL;
                    this.conn.notifyHalfClosed(this);
                }
                else if (this._state == Http2StreamState.HALF_CLOSED_LOCAL)
                {
                }
                else
                {
                    close();
                }

                return this;
            }

            public virtual Http2Stream closeRemoteSide()
            {
                if (_state == Http2StreamState.OPEN)
                {
                    _state = Http2StreamState.HALF_CLOSED_REMOTE;
                    this.conn.notifyHalfClosed(this);
                }
                else if (this._state == Http2StreamState.HALF_CLOSED_REMOTE)
                {
                }
                else
                {
                    this.close();
                }

                return this;
            }

            internal virtual DefaultEndpoint createdBy()
            {
                return this.conn.localEndpoint.isValidStreamId(_id)
                    ? (DefaultEndpoint)this.conn.localEndpoint
                    : this.conn.remoteEndpoint;
            }

            bool isLocal()
            {
                return this.conn.localEndpoint.isValidStreamId(_id);
            }

            /**
             * Provides the lazy initialization for the {@link DefaultStream} data map.
             */
            class PropertyMap
            {
                readonly PropertyKeyRegistry registry;
                object[] values = EmptyArrays.EmptyObjects;

                public PropertyMap(PropertyKeyRegistry registry)
                {
                    this.registry = registry;
                }

                internal V add<V>(DefaultPropertyKey key, V value)
                    where V : class
                {
                    resizeIfNecessary(key.index);
                    V prevValue = (V)values[key.index];
                    values[key.index] = value;
                    return prevValue;
                }

                internal V get<V>(DefaultPropertyKey key)
                    where V : class
                {
                    if (key.index >= values.Length)
                    {
                        return null;
                    }

                    return (V)values[key.index];
                }

                internal V remove<V>(DefaultPropertyKey key)
                    where V : class
                {
                    V prevValue = null;
                    if (key.index < values.Length)
                    {
                        prevValue = (V)values[key.index];
                        values[key.index] = null;
                    }

                    return prevValue;
                }

                void resizeIfNecessary(int index)
                {
                    if (index >= values.Length)
                    {
                        object[] tmp = new object[this.registry.size()];
                        Array.Copy(this.values, tmp, Math.Min(this.values.Length, tmp.Length));
                        values = tmp;
                    }
                }
            }
        }

        /**
         * Stream class representing the connection, itself.
         */
        sealed class ConnectionStream : DefaultStream
        {
            internal ConnectionStream(DefaultHttp2Connection conn)
                : base(conn, Http2CodecUtil.CONNECTION_STREAM_ID, Http2StreamState.IDLE)
            {
            }

            public override bool isResetSent()
            {
                return false;
            }

            internal override DefaultEndpoint createdBy()
            {
                return null;
            }

            public override Http2Stream resetSent()
            {
                throw new NotSupportedException();
            }

            public override Http2Stream open(bool halfClosed)
            {
                throw new NotSupportedException();
            }

            public override Http2Stream close()
            {
                throw new NotSupportedException();
            }

            public override Http2Stream closeLocalSide()
            {
                throw new NotSupportedException();
            }

            public override Http2Stream closeRemoteSide()
            {
                throw new NotSupportedException();
            }

            public override Http2Stream headersSent(bool isInformational)
            {
                throw new NotSupportedException();
            }

            public override bool isHeadersSent()
            {
                throw new NotSupportedException();
            }

            public override Http2Stream pushPromiseSent()
            {
                throw new NotSupportedException();
            }

            public override bool isPushPromiseSent()
            {
                throw new NotSupportedException();
            }
        }

        internal class DefaultEndpoint<F> : DefaultEndpoint, Http2ConnectionEndpoint<F>
            where F : class, Http2FlowController
        {
            F _flowController;

            internal DefaultEndpoint(DefaultHttp2Connection conn, bool server, int maxReservedStreams)
                : base(conn, server, maxReservedStreams)
            {
            }

            public F flowController()
            {
                return _flowController;
            }

            public void flowController(F flowController)
            {
                Contract.Requires(flowController != null);
                this._flowController = flowController;
            }
        }

        /**
         * Simple endpoint implementation.
         */
        internal class DefaultEndpoint : Http2ConnectionEndpoint
        {
            readonly DefaultHttp2Connection conn;

            readonly bool server;

            /**
             * The minimum stream ID allowed when creating the next stream. This only applies at the time the stream is
             * created. If the ID of the stream being created is less than this value, stream creation will fail. Upon
             * successful creation of a stream, this value is incremented to the next valid stream ID.
             */
            private int nextStreamIdToCreate;

            /**
             * Used for reservation of stream IDs. Stream IDs can be reserved in advance by applications before the streams
             * are actually created.  For example, applications may choose to buffer stream creation attempts as a way of
             * working around {@code SETTINGS_MAX_CONCURRENT_STREAMS}, in which case they will reserve stream IDs for each
             * buffered stream.
             */
            private int nextReservationStreamId;
            internal int _lastStreamKnownByPeer = -1;
            private bool pushToAllowed = true;

            private int maxStreams;
            private int _maxActiveStreams;

            private readonly int maxReservedStreams;

            // Fields accessed by inner classes
            internal int _numActiveStreams;
            internal int numStreams;

            internal DefaultEndpoint(DefaultHttp2Connection conn, bool server, int maxReservedStreams)
            {
                this.conn = conn;
                this.server = server;

                // Determine the starting stream ID for this endpoint. Client-initiated streams
                // are odd and server-initiated streams are even. Zero is reserved for the
                // connection. Stream 1 is reserved client-initiated stream for responding to an
                // upgrade from HTTP 1.1.
                if (server)
                {
                    nextStreamIdToCreate = 2;
                    nextReservationStreamId = 0;
                }
                else
                {
                    nextStreamIdToCreate = 1;
                    // For manually created client-side streams, 1 is reserved for HTTP upgrade, so start at 3.
                    nextReservationStreamId = 1;
                }

                // Push is disallowed by default for servers and allowed for clients.
                pushToAllowed = !server;
                _maxActiveStreams = int.MaxValue;
                Contract.Requires(maxReservedStreams >= 0);
                this.maxReservedStreams = maxReservedStreams;
                updateMaxStreams();
            }

            public int incrementAndGetNextStreamId()
            {
                return nextReservationStreamId >= 0 ? nextReservationStreamId += 2 : nextReservationStreamId;
            }

            private void incrementExpectedStreamId(int streamId)
            {
                if (streamId > nextReservationStreamId && nextReservationStreamId >= 0)
                {
                    nextReservationStreamId = streamId;
                }

                nextStreamIdToCreate = streamId + 2;
                ++numStreams;
            }

            public bool isValidStreamId(int streamId)
            {
                return streamId > 0 && server == ((streamId & 1) == 0);
            }

            public bool mayHaveCreatedStream(int streamId)
            {
                return isValidStreamId(streamId) && streamId <= lastStreamCreated();
            }

            public bool canOpenStream()
            {
                return _numActiveStreams < _maxActiveStreams;
            }

            public Http2Stream createStream(int streamId, bool halfClosed)
            {
                Http2StreamState state = activeState(streamId, Http2StreamState.IDLE, isLocal(), halfClosed);

                checkNewStreamAllowed(streamId, state);

                // Create and initialize the stream.
                DefaultStream stream = new DefaultStream(this.conn, streamId, state);

                incrementExpectedStreamId(streamId);

                addStream(stream);

                stream.activate();
                return stream;
            }

            public bool created(Http2Stream stream)
            {
                return stream is DefaultStream defaultStream && ReferenceEquals(defaultStream.createdBy(), this);
            }

            public bool isServer()
            {
                return server;
            }

            public Http2Stream reservePushStream(int streamId, Http2Stream parent)
            {
                if (parent == null)
                {
                    throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "Parent stream missing");
                }

                if (isLocal() ? !parent.state().localSideOpen() : !parent.state().remoteSideOpen())
                {
                    throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "Stream {0} is not open for sending push promise", parent.id());
                }

                if (!opposite().allowPushTo())
                {
                    throw Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "Server push not allowed to opposite endpoint");
                }

                Http2StreamState state = isLocal() ? Http2StreamState.RESERVED_LOCAL : Http2StreamState.RESERVED_REMOTE;
                checkNewStreamAllowed(streamId, state);

                // Create and initialize the stream.
                DefaultStream stream = new DefaultStream(this.conn, streamId, state);

                incrementExpectedStreamId(streamId);

                // Register the stream.
                addStream(stream);
                return stream;
            }

            private void addStream(DefaultStream stream)
            {
                // Add the stream to the map and priority tree.
                this.conn.streamMap.Add(stream.id(), stream);

                // Notify the listeners of the event.
                for (int i = 0; i < this.conn.listeners.Count; i++)
                {
                    try
                    {
                        this.conn.listeners[i].onStreamAdded(stream);
                    }
                    catch (Exception cause)
                    {
                        logger.Error("Caught Exception from listener onStreamAdded.", cause);
                    }
                }
            }

            public void allowPushTo(bool allow)
            {
                if (allow && server)
                {
                    throw new ArgumentException("Servers do not allow push");
                }

                pushToAllowed = allow;
            }

            public bool allowPushTo()
            {
                return pushToAllowed;
            }

            public int numActiveStreams()
            {
                return _numActiveStreams;
            }

            public int maxActiveStreams()
            {
                return _maxActiveStreams;
            }

            public void maxActiveStreams(int _maxActiveStreams)
            {
                this._maxActiveStreams = _maxActiveStreams;
                updateMaxStreams();
            }

            public int lastStreamCreated()
            {
                return nextStreamIdToCreate > 1 ? nextStreamIdToCreate - 2 : 0;
            }

            public int lastStreamKnownByPeer()
            {
                return _lastStreamKnownByPeer;
            }

            public void lastStreamKnownByPeer(int lastKnownStream)
            {
                this._lastStreamKnownByPeer = lastKnownStream;
            }

            public Http2ConnectionEndpoint opposite()
            {
                return this.isLocal() ? (Http2ConnectionEndpoint)this.conn.remoteEndpoint : this.conn.localEndpoint;
            }

            private void updateMaxStreams()
            {
                maxStreams = (int)Math.Min(int.MaxValue, (long)_maxActiveStreams + maxReservedStreams);
            }

            private void checkNewStreamAllowed(int streamId, Http2StreamState state)
            {
                Contract.Assert(state != Http2StreamState.IDLE);
                if (this.conn.goAwayReceived() && streamId > this.conn.localEndpoint.lastStreamKnownByPeer())
                {
                    throw Http2Exception.connectionError(
                        Http2Error.PROTOCOL_ERROR,
                        "Cannot create stream {0} since this endpoint has received a " +
                        "GOAWAY frame with last stream id {1}.",
                        streamId,
                        this.conn.localEndpoint.lastStreamKnownByPeer());
                }

                if (!isValidStreamId(streamId))
                {
                    if (streamId < 0)
                    {
                        throw new Http2NoMoreStreamIdsException();
                    }

                    throw Http2Exception.connectionError(
                        Http2Error.PROTOCOL_ERROR,
                        "Request stream {0} is not correct for {1} connection",
                        streamId,
                        server ? "server" : "client");
                }

                // This check must be after all id validated checks, but before the max streams check because it may be
                // recoverable to some degree for handling frames which can be sent on closed streams.
                if (streamId < nextStreamIdToCreate)
                {
                    throw Http2Exception.closedStreamError(
                        Http2Error.PROTOCOL_ERROR,
                        "Request stream {0} is behind the next expected stream {1}",
                        streamId,
                        nextStreamIdToCreate);
                }

                if (nextStreamIdToCreate <= 0)
                {
                    throw Http2Exception.connectionError(Http2Error.REFUSED_STREAM, "Stream IDs are exhausted for this endpoint.");
                }

                bool isReserved = state == Http2StreamState.RESERVED_LOCAL || state == Http2StreamState.RESERVED_REMOTE;
                if (!isReserved && !canOpenStream() || isReserved && numStreams >= maxStreams)
                {
                    throw Http2Exception.streamError(streamId, Http2Error.REFUSED_STREAM, "Maximum active streams violated for this endpoint.");
                }

                if (this.conn.isClosed())
                {
                    throw Http2Exception.connectionError(
                        Http2Error.INTERNAL_ERROR,
                        "Attempted to create stream id {0} after connection was closed",
                        streamId);
                }
            }

            bool isLocal()
            {
                return ReferenceEquals(this, this.conn.localEndpoint);
            }
        }

        /**
         * Manages the list of currently active streams.  Queues any {@link Action}s that would modify the list of
         * active streams in order to prevent modification while iterating.
         */
        sealed class ActiveStreams
        {
            readonly DefaultHttp2Connection conn;
            readonly List<Http2ConnectionListener> listeners;
            readonly IQueue<Action> pendingEvents = PlatformDependent.NewMpscQueue<Action>();
            readonly ISet<Http2Stream> streams = new HashSet<Http2Stream>();
            private int pendingIterations;

            public ActiveStreams(DefaultHttp2Connection conn, List<Http2ConnectionListener> listeners)
            {
                this.conn = conn;
                this.listeners = listeners;
            }

            public int size()
            {
                return streams.Count;
            }

            public void activate(DefaultStream stream)
            {
                if (allowModifications())
                {
                    addToActiveStreams(stream);
                }
                else
                {
                    pendingEvents.TryEnqueue(() => addToActiveStreams(stream));
                }
            }

            internal void deactivate(DefaultStream stream, bool force)
            {
                if (allowModifications() || force)
                {
                    this.removeFromActiveStreams(stream);
                }
                else
                {
                    pendingEvents.TryEnqueue(() => this.removeFromActiveStreams(stream));
                }
            }

            public Http2Stream forEachActiveStream(Func<Http2Stream, bool> visitor)
            {
                incrementPendingIterations();
                try
                {
                    foreach (Http2Stream stream in streams)
                    {
                        if (!visitor(stream))
                        {
                            return stream;
                        }
                    }

                    return null;
                }
                finally
                {
                    decrementPendingIterations();
                }
            }

            void addToActiveStreams(DefaultStream stream)
            {
                if (streams.Add(stream))
                {
                    // Update the number of active streams initiated by the endpoint.
                    stream.createdBy()._numActiveStreams++;

                    for (int i = 0; i < listeners.Count; i++)
                    {
                        try
                        {
                            listeners[i].onStreamActive(stream);
                        }
                        catch (Exception cause)
                        {
                            logger.Error("Caught Exception from listener onStreamActive.", cause);
                        }
                    }
                }
            }

            void removeFromActiveStreams(DefaultStream stream)
            {
                if (streams.Remove(stream))
                {
                    // Update the number of active streams initiated by the endpoint.
                    stream.createdBy()._numActiveStreams--;
                    this.conn.notifyClosed(stream);
                }

                this.conn.removeStream(stream);
            }

            internal bool allowModifications()
            {
                return pendingIterations == 0;
            }

            internal void incrementPendingIterations()
            {
                ++pendingIterations;
            }

            internal void decrementPendingIterations()
            {
                --pendingIterations;
                if (allowModifications())
                {
                    while (this.pendingEvents.TryDequeue(out Action evt))
                    {
                        try
                        {
                            evt();
                        }
                        catch (Exception cause)
                        {
                            logger.Error("Caught Exception while processing pending ActiveStreams$Event.", cause);
                        }
                    }
                }
            }
        }

        /**
         * Implementation of {@link Http2ConnectionPropertyKey} that specifies the index position of the property.
         */
        sealed class DefaultPropertyKey : Http2ConnectionPropertyKey
        {
            readonly DefaultHttp2Connection conn;
            internal readonly int index;

            internal DefaultPropertyKey(DefaultHttp2Connection conn, int index)
            {
                this.conn = conn;
                this.index = index;
            }

            internal DefaultPropertyKey verifyConnection(Http2Connection connection)
            {
                if (connection != this.conn)
                {
                    throw new ArgumentException("Using a key that was not created by this connection");
                }

                return this;
            }
        }

        /**
         * A registry of all stream property keys known by this connection.
         */
        sealed class PropertyKeyRegistry
        {
            /**
             * Initial size of 4 because the default configuration currently has 3 listeners
             * (local/remote flow controller and {@link StreamByteDistributor}) and we leave room for 1 extra.
             * We could be more aggressive but the ArrayList resize will double the size if we are too small.
             */
            readonly IList<DefaultPropertyKey> keys = new List<DefaultPropertyKey>(4);

            /**
             * Registers a new property key.
             */
            internal DefaultPropertyKey newKey(DefaultHttp2Connection conn)
            {
                DefaultPropertyKey key = new DefaultPropertyKey(conn, keys.Count);
                keys.Add(key);
                return key;
            }

            internal int size()
            {
                return keys.Count;
            }
        }
    }
}