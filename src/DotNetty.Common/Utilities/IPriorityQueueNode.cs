// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    /**
     * Provides methods for {@link DefaultPriorityQueue} to maintain internal state. These methods should generally not be
     * used outside the scope of {@link DefaultPriorityQueue}.
     */
    public interface IPriorityQueueNode<T>
        where T : class, IPriorityQueueNode<T>
    {
        /**
         * Get the last value set by {@link #priorityQueueIndex(DefaultPriorityQueue, int)} for the value corresponding to
         * {@code queue}.
         * <p>
         * Throwing exceptions from this method will result in undefined behavior.
         */
        int GetPriorityQueueIndex(IPriorityQueue<T> queue);

        /**
          * Used by {@link DefaultPriorityQueue} to maintain state for an element in the queue.
          * <p>
          * Throwing exceptions from this method will result in undefined behavior.
          * @param queue The queue for which the index is being set.
          * @param i The index as used by {@link DefaultPriorityQueue}.
          */
        void SetPriorityQueueIndex(IPriorityQueue<T> queue, int i);
    }
}