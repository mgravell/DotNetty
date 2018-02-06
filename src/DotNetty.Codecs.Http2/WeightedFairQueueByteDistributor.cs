
namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections.Concurrent;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Linq;
    using System.Text;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    /**
     * A {@link StreamByteDistributor} that is sensitive to stream priority and uses
     * <a href="https://en.wikipedia.org/wiki/Weighted_fair_queueing">Weighted Fair Queueing</a> approach for distributing
     * bytes.
     * <p>
     * Inspiration for this distributor was taken from Linux's
     * <a href="https://www.kernel.org/doc/Documentation/scheduler/sched-design-CFS.txt">Completely Fair Scheduler</a>
     * to model the distribution of bytes to simulate an "ideal multi-tasking CPU", but in this case we are simulating
     * an "ideal multi-tasking NIC".
     * <p>
     * Each write operation will use the {@link #allocationQuantum(int)} to know how many more bytes should be allocated
     * relative to the next stream which wants to write. This is to balance fairness while also considering goodput.
     */
    public sealed class WeightedFairQueueByteDistributor : StreamByteDistributor 
    {
        /**
     * The initial size of the children map is chosen to be conservative on initial memory allocations under
     * the assumption that most streams will have a small number of children. This choice may be
     * sub-optimal if when children are present there are many children (i.e. a web page which has many
     * dependencies to load).
     *
     * Visible only for testing!
     */
        static readonly int INITIAL_CHILDREN_MAP_SIZE = Math.Max(1, SystemPropertyUtil.GetInt("io.netty.http2.childrenMapSize", 2));
        /**
     * FireFox currently uses 5 streams to establish QoS classes.
     */
        const int DEFAULT_MAX_STATE_ONLY_SIZE = 5;

        readonly Http2ConnectionPropertyKey stateKey;
        /**
     * If there is no Http2Stream object, but we still persist priority information then this is where the state will
     * reside.
     */
        readonly ConcurrentDictionary<int, State> stateOnlyMap;
        /**
     * This queue will hold streams that are not active and provides the capability to retain priority for streams which
     * have no {@link Http2Stream} object. See {@link StateOnlyComparator} for the priority comparator.
     */
        readonly PriorityQueue<State> stateOnlyRemovalQueue;
        readonly Http2Connection connection;
        readonly State connectionState;
        /**
     * The minimum number of bytes that we will attempt to allocate to a stream. This is to
     * help improve goodput on a per-stream basis.
     */
        int allocationQuantum = Http2CodecUtil.DEFAULT_MIN_ALLOCATION_CHUNK;
        readonly int maxStateOnlySize;

        public WeightedFairQueueByteDistributor(Http2Connection connection) : this(connection, DEFAULT_MAX_STATE_ONLY_SIZE)
        {
            
        }

        public WeightedFairQueueByteDistributor(Http2Connection connection, int maxStateOnlySize) {
            if (maxStateOnlySize < 0) {
                throw new ArgumentException("maxStateOnlySize: " + maxStateOnlySize + " (expected: >0)");
            } else if (maxStateOnlySize == 0) {
                stateOnlyMap = IntCollections.emptyMap();
                stateOnlyRemovalQueue = EmptyPriorityQueue.instance();
            } else {
                stateOnlyMap = new ConcurrentDictionary<int, State>();
                // +2 because we may exceed the limit by 2 if a new dependency has no associated Http2Stream object. We need
                // to create the State objects to put them into the dependency tree, which then impacts priority.
                stateOnlyRemovalQueue = new PriorityQueue<State>(StateOnlyComparator.INSTANCE, maxStateOnlySize + 2);
            }
            this.maxStateOnlySize = maxStateOnlySize;

            this.connection = connection;
            stateKey = connection.newKey();
            Http2Stream connectionStream = connection.connectionStream();
            connectionStream.setProperty(stateKey, connectionState = new State(connectionStream, 16));

            // Register for notification of new streams.
            connection.addListener(new StateTracker(this));
        }
        
        class StateTracker : Http2ConnectionAdapter 
        {
            readonly WeightedFairQueueByteDistributor parent;

            public StateTracker(WeightedFairQueueByteDistributor parent)
            {
                this.parent = parent;
            }
            
            public override void onStreamAdded(Http2Stream stream) 
            {
                if (!this.parent.stateOnlyMap.TryRemove(stream.id(), out State state)) 
                {
                    state = new State(stream);
                    // Only the stream which was just added will change parents. So we only need an array of size 1.
                    List<ParentChangedEvent> events = new List<ParentChangedEvent>(1);
                    this.parent.connectionState.takeChild(state, false, events);
                    this.parent.notifyParentChanged(events);
                } 
                else 
                {
                    this.parent.stateOnlyRemovalQueue.Remove(state);
                    state.stream = stream;
                }
                switch (stream.state()) 
                {
                    case Http2StreamState.RESERVED_REMOTE:
                    case Http2StreamState.RESERVED_LOCAL:
                        state.setStreamReservedOrActivated();
                        // wasStreamReservedOrActivated is part of the comparator for stateOnlyRemovalQueue there is no
                        // need to reprioritize here because it will not be in stateOnlyRemovalQueue.
                        break;
                    default:
                        break;
                }
                
                stream.setProperty(this.parent.stateKey, state);
            }

            public override void onStreamActive(Http2Stream stream) {
                this.parent.state(stream).setStreamReservedOrActivated();
                // wasStreamReservedOrActivated is part of the comparator for stateOnlyRemovalQueue there is no need to
                // reprioritize here because it will not be in stateOnlyRemovalQueue.
            }

            public override void onStreamClosed(Http2Stream stream) 
            {
                this.parent.state(stream).close();
            }

            public override void onStreamRemoved(Http2Stream stream) 
            {
                // The stream has been removed from the connection. We can no longer rely on the stream's property
                // storage to track the State. If we have room, and the precedence of the stream is sufficient, we
                // should retain the State in the stateOnlyMap.
                State state = this.parent.state(stream);

                // Typically the stream is set to null when the stream is closed because it is no longer needed to write
                // data. However if the stream was not activated it may not be closed (reserved streams) so we ensure
                // the stream reference is set to null to avoid retaining a reference longer than necessary.
                state.stream = null;

                if (this.parent.maxStateOnlySize == 0) 
                {
                    state.parent.removeChild(state);
                    return;
                }
                
                if (this.parent.stateOnlyRemovalQueue.Count == this.parent.maxStateOnlySize) 
                {
                    State stateToRemove = this.parent.stateOnlyRemovalQueue.Peek();
                    if (StateOnlyComparator.INSTANCE.compare(stateToRemove, state) >= 0) 
                    {
                        // The "lowest priority" stream is a "higher priority" than the stream being removed, so we
                        // just discard the state.
                        state.parent.removeChild(state);
                        return;
                    }
                    this.parent.stateOnlyRemovalQueue.Dequeue();
                    stateToRemove.parent.removeChild(stateToRemove);
                    this.parent.stateOnlyMap.TryRemove(stateToRemove.streamId, out var _);
                }
                this.parent.stateOnlyRemovalQueue.Enqueue(state);
                this.parent.stateOnlyMap.TryAdd(state.streamId, state);
            }
        }        

        public void updateStreamableBytes(StreamByteDistributorContext state) {
            state(state.stream()).updateStreamableBytes(streamableBytes(state),
                state.hasFrame() && state.windowSize() >= 0);
        }

        public void updateDependencyTree(int childStreamId, int parentStreamId, short weight, bool exclusive) {
            State state = state(childStreamId);
            if (state == null) {
                // If there is no State object that means there is no Http2Stream object and we would have to keep the
                // State object in the stateOnlyMap and stateOnlyRemovalQueue. However if maxStateOnlySize is 0 this means
                // stateOnlyMap and stateOnlyRemovalQueue are empty collections and cannot be modified so we drop the State.
                if (this.maxStateOnlySize == 0) {
                    return;
                }
                state = new State(childStreamId);
                stateOnlyRemovalQueue.add(state);
                stateOnlyMap.put(childStreamId, state);
            }

            State newParent = state(parentStreamId);
            if (newParent == null) {
                // If there is no State object that means there is no Http2Stream object and we would have to keep the
                // State object in the stateOnlyMap and stateOnlyRemovalQueue. However if maxStateOnlySize is 0 this means
                // stateOnlyMap and stateOnlyRemovalQueue are empty collections and cannot be modified so we drop the State.
                if (this.maxStateOnlySize == 0) {
                    return;
                }
                newParent = new State(parentStreamId);
                stateOnlyRemovalQueue.add(newParent);
                stateOnlyMap.put(parentStreamId, newParent);
                // Only the stream which was just added will change parents. So we only need an array of size 1.
                List<ParentChangedEvent> events = new List<ParentChangedEvent>(1);
                connectionState.takeChild(newParent, false, events);
                notifyParentChanged(events);
            }

            // if activeCountForTree == 0 then it will not be in its parent's pseudoTimeQueue and thus should not be counted
            // toward parent.totalQueuedWeights.
            if (state.activeCountForTree != 0 && state.parent != null) {
                state.parent.totalQueuedWeights += weight - state.weight;
            }
            state.weight = weight;

            if (newParent != state.parent || (exclusive && newParent.children.Count != 1)) {
                List<ParentChangedEvent> events;
                if (newParent.isDescendantOf(state)) {
                    events = new List<ParentChangedEvent>(2 + (exclusive ? newParent.children.Count : 0));
                    state.parent.takeChild(newParent, false, events);
                } else {
                    events = new List<ParentChangedEvent>(1 + (exclusive ? newParent.children.Count : 0));
                }
                newParent.takeChild(state, exclusive, events);
                notifyParentChanged(events);
            }

            // The location in the dependency tree impacts the priority in the stateOnlyRemovalQueue map. If we created new
            // State objects we must check if we exceeded the limit after we insert into the dependency tree to ensure the
            // stateOnlyRemovalQueue has been updated.
            while (stateOnlyRemovalQueue.Count > this.maxStateOnlySize) {
                State stateToRemove = stateOnlyRemovalQueue.Dequeue();
                stateToRemove.parent.removeChild(stateToRemove);
                stateOnlyMap.remove(stateToRemove.streamId);
            }
        }

        public bool distribute(int maxBytes, Writer writer)  {
            // As long as there is some active frame we should write at least 1 time.
        if (connectionState.activeCountForTree == 0) {
            return false;
        }

        // The goal is to write until we write all the allocated bytes or are no longer making progress.
        // We still attempt to write even after the number of allocated bytes has been exhausted to allow empty frames
        // to be sent. Making progress means the active streams rooted at the connection stream has changed.
        int oldIsActiveCountForTree;
            do {
            oldIsActiveCountForTree = connectionState.activeCountForTree;
            // connectionState will never be active, so go right to its children.
            maxBytes -= distributeToChildren(maxBytes, writer, connectionState);
        } while (connectionState.activeCountForTree != 0 && (maxBytes > 0 || oldIsActiveCountForTree != connectionState.activeCountForTree));

        return connectionState.activeCountForTree != 0;
    }

    /**
     * Sets the amount of bytes that will be allocated to each stream. Defaults to 1KiB.
     * @param allocationQuantum the amount of bytes that will be allocated to each stream. Must be &gt; 0.
     */
    public void allocationQuantum(int allocationQuantum) {
        if (allocationQuantum <= 0) {
            throw new ArgumentException("allocationQuantum must be > 0");
        }
        this.allocationQuantum = allocationQuantum;
    }

    int distribute(int maxBytes, Writer writer, State state)  {
        if (state.isActive()) {
            int nsent = min(maxBytes, state.streamableBytes);
            state.write(nsent, writer);
            if (nsent == 0 && maxBytes != 0) {
                // If a stream sends zero bytes, then we gave it a chance to write empty frames and it is now
                // considered inactive until the next call to updateStreamableBytes. This allows descendant streams to
                // be allocated bytes when the parent stream can't utilize them. This may be as a result of the
                // stream's flow control window being 0.
                state.updateStreamableBytes(state.streamableBytes, false);
            }
            return nsent;
        }

        return distributeToChildren(maxBytes, writer, state);
    }

    /**
     * It is a pre-condition that {@code state.poll()} returns a non-{@code null} value. This is a result of the way
     * the allocation algorithm is structured and can be explained in the following cases:
     * <h3>For the recursive case</h3>
     * If a stream has no children (in the allocation tree) than that node must be active or it will not be in the
     * allocation tree. If a node is active then it will not delegate to children and recursion ends.
     * <h3>For the initial case</h3>
     * We check connectionState.activeCountForTree == 0 before any allocation is done. So if the connection stream
     * has no active children we don't get into this method.
     */
    private int distributeToChildren(int maxBytes, Writer writer, State state)  {
        long oldTotalQueuedWeights = state.totalQueuedWeights;
        State childState = state.pollPseudoTimeQueue();
        State nextChildState = state.peekPseudoTimeQueue();
        childState.setDistributing();
        try {
            assert nextChildState == null || nextChildState.pseudoTimeToWrite >= childState.pseudoTimeToWrite :
                "nextChildState[" + nextChildState.streamId + "].pseudoTime(" + nextChildState.pseudoTimeToWrite +
                ") < " + " childState[" + childState.streamId + "].pseudoTime(" + childState.pseudoTimeToWrite + ")";
            int nsent = distribute(nextChildState == null ? maxBytes :
                            min(maxBytes, (int) min((nextChildState.pseudoTimeToWrite - childState.pseudoTimeToWrite) *
                                               childState.weight / oldTotalQueuedWeights + allocationQuantum, MAX_VALUE)
                               ),
                               writer,
                               childState);
            state.pseudoTime += nsent;
            childState.updatePseudoTime(state, nsent, oldTotalQueuedWeights);
            return nsent;
        } finally {
            childState.unsetDistributing();
            // Do in finally to ensure the internal flags is not corrupted if an exception is thrown.
            // The offer operation is delayed until we unroll up the recursive stack, so we don't have to remove from
            // the priority pseudoTimeQueue due to a write operation.
            if (childState.activeCountForTree != 0) {
                state.offerPseudoTimeQueue(childState);
            }
        }
    }

    private State state(Http2Stream stream) {
        return stream.getProperty(stateKey);
    }

    private State state(int streamId) {
        Http2Stream stream = connection.stream(streamId);
        return stream != null ? state(stream) : stateOnlyMap.get(streamId);
    }

    /**
     * For testing only!
     */
    bool isChild(int childId, int parentId, short weight) {
        State parent = state(parentId);
        State child;
        return parent.children.ContainsKey(childId) &&
                (child = state(childId)).parent == parent && child.weight == weight;
    }

    /**
     * For testing only!
     */
    int numChildren(int streamId) {
        State state = state(streamId);
        return state == null ? 0 : state.children.Count;
    }

    /**
     * Notify all listeners of the priority tree change events (in ascending order)
     * @param events The events (top down order) which have changed
     */
    void notifyParentChanged(List<ParentChangedEvent> events) {
        for (int i = 0; i < events.Count; ++i) 
        {
            ParentChangedEvent @event = events[i];
            
            stateOnlyRemovalQueue.PriorityChanged(@event.state);
            if (@event.state.parent != null && @event.state.activeCountForTree != 0) 
            {
                @event.state.parent.offerAndInitializePseudoTime(@event.state);
                @event.state.parent.activeCountChangeForTree(@event.state.activeCountForTree);
            }
        }
    }

    /**
     * A comparator for {@link State} which has no associated {@link Http2Stream} object. The general precedence is:
     * <ul>
     *     <li>Was a stream activated or reserved (streams only used for priority are higher priority)</li>
     *     <li>Depth in the priority tree (closer to root is higher priority></li>
     *     <li>Stream ID (higher stream ID is higher priority - used for tie breaker)</li>
     * </ul>
     */
    sealed class StateOnlyComparator : IComparer<State>, ISerializable 
    {
        static readonly long serialVersionUID = -4806936913002105966L;

        internal static readonly StateOnlyComparator INSTANCE = new StateOnlyComparator();

        private StateOnlyComparator() {
        }

        public int compare(State o1, State o2) {
            // "priority only streams" (which have not been activated) are higher priority than streams used for data.
            bool o1Actived = o1.wasStreamReservedOrActivated();
            
            if (o1Actived != o2.wasStreamReservedOrActivated()) 
            {
                return o1Actived ? -1 : 1;
            }
            
            // Numerically greater depth is higher priority.
            int x = o2.dependencyTreeDepth - o1.dependencyTreeDepth;

            // I also considered tracking the number of streams which are "activated" (eligible transfer data) at each
            // subtree. This would require a traversal from each node to the root on dependency tree structural changes,
            // and then it would require a re-prioritization at each of these nodes (instead of just the nodes where the
            // direct parent changed). The costs of this are judged to be relatively high compared to the nominal
            // benefit it provides to the heuristic. Instead folks should just increase maxStateOnlySize.

            // Last resort is to give larger stream ids more priority.
            return x != 0 ? x : o1.streamId - o2.streamId;
        }
    }

    sealed class StatePseudoTimeComparator : IComparer<State>, Serializable 
    {
        static readonly long serialVersionUID = -1437548640227161828L;

        internal static readonly StatePseudoTimeComparator INSTANCE = new StatePseudoTimeComparator();

        private StatePseudoTimeComparator() {
        }

        public int Compare(State o1, State o2) {
            
            return MathUtil.Compare(o1.pseudoTimeToWrite, o2.pseudoTimeToWrite);
        }
    }

    /**
     * The remote flow control state for a single stream.
     */
    class State : IPriorityQueueNode<State>
    {
        const int INDEX_NOT_IN_QUEUE = -1;
        
        static readonly byte STATE_IS_ACTIVE = 0x1;
        static readonly byte STATE_IS_DISTRIBUTING = 0x2;
        static readonly byte STATE_STREAM_ACTIVATED = 0x4;

        /**
         * Maybe {@code null} if the stream if the stream is not active.
         */
        internal Http2Stream stream;
        internal State parent;
        
        //ConcurrentDictionary<int, State> children = IntCollections.emptyMap();

        internal ConcurrentDictionary<int, State> children = new ConcurrentDictionary<int, State>();
        
        readonly PriorityQueue<State> pseudoTimeQueue;
        internal readonly int streamId;
        int streamableBytes;

        internal int dependencyTreeDepth;
        /**
         * Count of nodes rooted at this sub tree with {@link #isActive()} equal to {@code true}.
         */
        internal int activeCountForTree;
        int pseudoTimeQueueIndex = INDEX_NOT_IN_QUEUE;
        int stateOnlyQueueIndex = INDEX_NOT_IN_QUEUE;
        /**
         * An estimate of when this node should be given the opportunity to write data.
         */
        internal long pseudoTimeToWrite;
        /**
         * A pseudo time maintained for immediate children to base their {@link #pseudoTimeToWrite} off of.
         */
        long pseudoTime;
        long totalQueuedWeights;
        private byte flags;
        short weight = Http2CodecUtil.DEFAULT_PRIORITY_WEIGHT;

        internal State(int streamId) :this(streamId, null, 0) {
            
        }

        internal State(Http2Stream stream) : this(stream, 0){
            
        }

        internal State(Http2Stream stream, int initialSize) : this(stream.id(), stream, initialSize){
            
        }

        internal State(int streamId, Http2Stream stream, int initialSize) 
        {
            this.stream = stream;
            this.streamId = streamId;
            pseudoTimeQueue = new PriorityQueue<State>(StatePseudoTimeComparator.INSTANCE, initialSize);
        }

        internal bool isDescendantOf(State state) {
            State next = parent;
            while (next != null) {
                if (next == state) {
                    return true;
                }
                next = next.parent;
            }
            return false;
        }

        void takeChild(State child, bool exclusive, List<ParentChangedEvent> events) {
            takeChild(null, child, exclusive, events);
        }

        /**
         * Adds a child to this priority. If exclusive is set, any children of this node are moved to being dependent on
         * the child.
         */
        void takeChild(IEnumerator<KeyValuePair<int, State>> childItr, State child, bool exclusive, List<ParentChangedEvent> events) {
            State oldParent = child.parent;

            if (oldParent != this) 
            {
                events.Add(new ParentChangedEvent(child, oldParent));
                child.setParent(this);
                
                // If the childItr is not null we are iterating over the oldParent.children collection and should
                // use the iterator to remove from the collection to avoid concurrent modification. Otherwise it is
                // assumed we are not iterating over this collection and it is safe to call remove directly.
                if (childItr != null) 
                {
                    childItr.remove();
                } 
                else if (oldParent != null) 
                {
                    oldParent.children.TryRemove(child.streamId, out var _);
                }

                // Lazily initialize the children to save object allocations.
                initChildrenIfEmpty();

                var added = children.TryAdd(child.streamId, child);
                Contract.Assert(added,"A stream with the same stream ID was already in the child map.");
            }

            if (exclusive && !children.IsEmpty)
            {
                
                // If it was requested that this child be the exclusive dependency of this node,
                // move any previous children to the child node, becoming grand children of this node.
                IEnumerator<KeyValuePair<int, State>> itr = removeAllChildrenExcept(child).GetEnumerator();
                while (itr.MoveNext()) 
                {
                    child.takeChild(itr, itr.Current.Value, false, events);
                }
            }
        }

        /**
         * Removes the child priority and moves any of its dependencies to being direct dependencies on this node.
         */
        internal void removeChild(State child) 
        {
            if (children.TryRemove(child.streamId, out var _) != null) 
            {
                List<ParentChangedEvent> events = new List<ParentChangedEvent>(1 + child.children.Count);
                events.Add(new ParentChangedEvent(child, child.parent));
                child.setParent(null);

                // Move up any grand children to be directly dependent on this node.
                IEnumerator<KeyValuePair<int, State>> itr = child.children.GetEnumerator();
                while (itr.MoveNext()) {
                    takeChild(itr, itr.Current.Value, false, events);
                }

                notifyParentChanged(events);
            }
        }

        /**
         * Remove all children with the exception of {@code streamToRetain}.
         * This method is intended to be used to support an exclusive priority dependency operation.
         * @return The map of children prior to this operation, excluding {@code streamToRetain} if present.
         */
        ConcurrentDictionary<int, State> removeAllChildrenExcept(State stateToRetain)
        {

            var removed = this.children.TryRemove(stateToRetain.streamId, out stateToRetain);
            var prevChildren = children;
            // This map should be re-initialized in anticipation for the 1 exclusive child which will be added.
            // It will either be added directly in this method, or after this method is called...but it will be added.
            initChildren();
            if (removed) 
            {
                children.TryAdd(stateToRetain.streamId, stateToRetain);
            }
            return prevChildren;
        }

        private void setParent(State newParent) {
            // if activeCountForTree == 0 then it will not be in its parent's pseudoTimeQueue.
            if (activeCountForTree != 0 && parent != null) {
                parent.removePseudoTimeQueue(this);
                parent.activeCountChangeForTree(-activeCountForTree);
            }
            parent = newParent;
            // Use MAX_VALUE if no parent because lower depth is considered higher priority by StateOnlyComparator.
            dependencyTreeDepth = newParent == null ? MAX_VALUE : newParent.dependencyTreeDepth + 1;
        }

        private void initChildrenIfEmpty() {
            if (children == IntCollections.<State>emptyMap()) {
                initChildren();
            }
        }

        private void initChildren() {
            children = new ConcurrentDictionary<int, State>(INITIAL_CHILDREN_MAP_SIZE);
        }

        void write(int numBytes, Writer writer)  {
            assert stream != null;
            try {
                writer.write(stream, numBytes);
            } catch (Exception t) {
                throw connectionError(INTERNAL_ERROR, t, "byte distribution write error");
            }
        }

        internal void activeCountChangeForTree(int increment) {
            assert activeCountForTree + increment >= 0;
            activeCountForTree += increment;
            if (parent != null) {
                assert activeCountForTree != increment ||
                       pseudoTimeQueueIndex == INDEX_NOT_IN_QUEUE ||
                       parent.pseudoTimeQueue.Contains(this) :
                     "State[" + streamId + "].activeCountForTree changed from 0 to " + increment + " is in a " +
                     "pseudoTimeQueue, but not in parent[ " + parent.streamId + "]'s pseudoTimeQueue";
                if (activeCountForTree == 0) {
                    parent.removePseudoTimeQueue(this);
                } else if (activeCountForTree == increment && !isDistributing()) {
                    // If frame count was 0 but is now not, and this node is not already in a pseudoTimeQueue (assumed
                    // to be pState's pseudoTimeQueue) then enqueue it. If this State object is being processed the
                    // pseudoTime for this node should not be adjusted, and the node will be added back to the
                    // pseudoTimeQueue/tree structure after it is done being processed. This may happen if the
                    // activeCountForTree == 0 (a node which can't stream anything and is blocked) is at/near root of
                    // the tree, and is popped off the pseudoTimeQueue during processing, and then put back on the
                    // pseudoTimeQueue because a child changes position in the priority tree (or is closed because it is
                    // not blocked and finished writing all data).
                    parent.offerAndInitializePseudoTime(this);
                }
                parent.activeCountChangeForTree(increment);
            }
        }

        void updateStreamableBytes(int newStreamableBytes, bool isActive) {
            if (isActive() != isActive) {
                if (isActive) {
                    activeCountChangeForTree(1);
                    setActive();
                } else {
                    activeCountChangeForTree(-1);
                    unsetActive();
                }
            }

            streamableBytes = newStreamableBytes;
        }

        /**
         * Assumes the parents {@link #totalQueuedWeights} includes this node's weight.
         */
        void updatePseudoTime(State parentState, int nsent, long totalQueuedWeights) {
            assert streamId != CONNECTION_STREAM_ID && nsent >= 0;
            // If the current pseudoTimeToSend is greater than parentState.pseudoTime then we previously over accounted
            // and should use parentState.pseudoTime.
            pseudoTimeToWrite = min(pseudoTimeToWrite, parentState.pseudoTime) + nsent * totalQueuedWeights / weight;
        }

        /**
         * The concept of pseudoTime can be influenced by priority tree manipulations or if a stream goes from "active"
         * to "non-active". This method accounts for that by initializing the {@link #pseudoTimeToWrite} for
         * {@code state} to {@link #pseudoTime} of this node and then calls {@link #offerPseudoTimeQueue(State)}.
         */
        internal void offerAndInitializePseudoTime(State state) {
            state.pseudoTimeToWrite = pseudoTime;
            offerPseudoTimeQueue(state);
        }

        void offerPseudoTimeQueue(State state) {
            pseudoTimeQueue.offer(state);
            totalQueuedWeights += state.weight;
        }

        /**
         * Must only be called if the pseudoTimeQueue is non-empty!
         */
        State pollPseudoTimeQueue() {
            State state = pseudoTimeQueue.Dequeue();
            // This method is only ever called if the pseudoTimeQueue is non-empty.
            totalQueuedWeights -= state.weight;
            return state;
        }

        void removePseudoTimeQueue(State state) {
            if (pseudoTimeQueue.Remove(state)) {
                totalQueuedWeights -= state.weight;
            }
        }

        State peekPseudoTimeQueue() {
            return pseudoTimeQueue.Peek();
        }

        internal void close() {
            updateStreamableBytes(0, false);
            stream = null;
        }

        internal bool wasStreamReservedOrActivated() {
            return (flags & STATE_STREAM_ACTIVATED) != 0;
        }

        internal void setStreamReservedOrActivated() {
            flags |= STATE_STREAM_ACTIVATED;
        }

        bool isActive() {
            return (flags & STATE_IS_ACTIVE) != 0;
        }

        private void setActive() {
            flags |= STATE_IS_ACTIVE;
        }

        private void unsetActive() {
            flags &= ~STATE_IS_ACTIVE;
        }

        bool isDistributing() {
            return (flags & STATE_IS_DISTRIBUTING) != 0;
        }

        void setDistributing() {
            flags |= STATE_IS_DISTRIBUTING;
        }

        void unsetDistributing() {
            flags &= ~STATE_IS_DISTRIBUTING;
        }

        public int GetPriorityQueueIndex(PriorityQueue<State> queue) {
            return queue == stateOnlyRemovalQueue ? stateOnlyQueueIndex : pseudoTimeQueueIndex;
        }

        public void SetPriorityQueueIndex(PriorityQueue<State> queue, int i) {
            if (queue == stateOnlyRemovalQueue) {
                stateOnlyQueueIndex = i;
            } else {
                pseudoTimeQueueIndex = i;
            }
        }

        
        public override string ToString() {
            // Use activeCountForTree as a rough estimate for how many nodes are in this subtree.
            StringBuilder sb = new StringBuilder(256 * (activeCountForTree > 0 ? activeCountForTree : 1));
            toString(sb);
            return sb.ToString();
        }

        void toString(StringBuilder sb) {
            sb.Append("{streamId ").Append(streamId)
                    .Append(" streamableBytes ").Append(streamableBytes)
                    .Append(" activeCountForTree ").Append(activeCountForTree)
                    .Append(" pseudoTimeQueueIndex ").Append(pseudoTimeQueueIndex)
                    .Append(" pseudoTimeToWrite ").Append(pseudoTimeToWrite)
                    .Append(" pseudoTime ").Append(pseudoTime)
                    .Append(" flags ").Append(flags)
                    .Append(" pseudoTimeQueue.Count ").Append(pseudoTimeQueue.Count)
                    .Append(" stateOnlyQueueIndex ").Append(stateOnlyQueueIndex)
                    .Append(" parent.streamId ").Append(parent == null ? -1 : parent.streamId).Append("} [");

            if (pseudoTimeQueue.Count > 0) 
            {

                foreach (var s in pseudoTimeQueue)
                {
                    s.toString(sb);
                    sb.Append(", ");
                }
                // Remove the last ", "
                sb.Length = sb.Length - 2;
            }
            sb.Append(']');
        }
    }

    /**
     * Allows a correlation to be made between a stream and its old parent before a parent change occurs.
     */
        
    //struct?
    sealed class ParentChangedEvent 
    {
        internal readonly State state;
        readonly State oldParent;

        /**
         * Create a new instance.
         * @param state The state who has had a parent change.
         * @param oldParent The previous parent.
         */
        internal ParentChangedEvent(State state, State oldParent) {
            this.state = state;
            this.oldParent = oldParent;
        }
    }
}
