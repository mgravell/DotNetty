// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Utilities
{
    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Reflection;
    using DotNetty.Common.Internal;

    public interface IPriorityQueue<T> : IQueue<T>
        where T : class, IPriorityQueueNode<T>
    {
        bool TryRemove(T item);

        bool Contains(T item);

        bool PriorityChanged(T item);
    }

    public class PriorityQueue<T> : IEnumerable<T>
        where T : class, IPriorityQueueNode<T>

    {
        static readonly T[] EmptyArray = new T[0];
        
        public const int IndexNotInQueue = -1;
        
        readonly IComparer<T> comparer;
        int count;
        int capacity;
        T[] items;

        public PriorityQueue(IComparer<T> comparer, int initialCapacity)
        {
            Contract.Requires(comparer != null);

            this.comparer = comparer;
            this.capacity = initialCapacity;
            this.items = this.capacity != 0 ? new T[this.capacity] : EmptyArray;
        }

        public PriorityQueue(IComparer<T> comparer)
            : this(comparer, 11)
        {
            
        }

        public PriorityQueue()
            : this(Comparer<T>.Default)
        {
        }

        public int Count => this.count;

        public T Dequeue()
        {
            T result = this.Peek();
            if (result == null)
            {
                return null;
            }
            
            result.SetPriorityQueueIndex(this, IndexNotInQueue);
            int newCount = --this.count;
            T lastItem = this.items[newCount];
            this.items[newCount] = null;
            if (newCount > 0)
            {
                this.TrickleDown(0, lastItem);
            }

            return result;
        }

        public T Peek() => this.count == 0 ? null : this.items[0];

        public void Enqueue(T item)
        {
            Contract.Requires(item != null);

            int index = item.GetPriorityQueueIndex(this);
            if (index != IndexNotInQueue)
            {
                throw new ArgumentException($"item.priorityQueueIndex(): {index} (expected: {IndexNotInQueue}) + item: {item}");
            }
            

            int oldCount = this.count;
            if (oldCount == this.capacity)
            {
                this.GrowHeap();
            }
            this.count = oldCount + 1;
            this.BubbleUp(oldCount, item);
        }

        public bool Remove(T item)
        {
            int index = item.GetPriorityQueueIndex(this);
            if (!this.Contains(item, index))
            {
                return false;
            }
            
            item.SetPriorityQueueIndex(this, IndexNotInQueue);

            this.count--;
            if (index == this.count)
            {
                this.items[index] = default(T);
            }
            else
            {
                T last = this.items[this.count];
                this.items[this.count] = default(T);
                this.TrickleDown(index, last);
                if (this.items[index] == last)
                {
                    this.BubbleUp(index, last);
                }
            }

            return true;
        }

        public bool Contains(T item)
            => this.Contains(item, item.GetPriorityQueueIndex(this));
        
        public void PriorityChanged(T node) 
        {
            int i = node.GetPriorityQueueIndex(this);
            if (!this.Contains(node, i)) 
            {
                return;
            }

            // Preserve the min-heap property by comparing the new priority with parents/children in the heap.
            if (i == 0) 
            {
                this.TrickleDown(i, node);
            } 
            else 
            {
                // Get the parent to see if min-heap properties are violated.
                int parentIndex = (i - 1) >> 1;
                T parent = this.items[parentIndex];
                if (this.comparer.Compare(node, parent) < 0) 
                {
                    this.BubbleUp(i, node);
                } 
                else 
                {
                    this.TrickleDown(i, node);
                }
            }
        }

        void BubbleUp(int index, T item)
        {
            // index > 0 means there is a parent
            while (index > 0)
            {
                int parentIndex = (index - 1) >> 1;
                T parentItem = this.items[parentIndex];
                if (this.comparer.Compare(item, parentItem) >= 0)
                {
                    break;
                }
                this.items[index] = parentItem;
                parentItem.SetPriorityQueueIndex(this, index);
                index = parentIndex;
            }
            
            this.items[index] = item;
            item.SetPriorityQueueIndex(this, index);
        }

        void GrowHeap()
        {
            int oldCapacity = this.capacity;
            this.capacity = oldCapacity + (oldCapacity <= 64 ? oldCapacity + 2 : (oldCapacity >> 1));
            var newHeap = new T[this.capacity];
            Array.Copy(this.items, 0, newHeap, 0, this.count);
            this.items = newHeap;
        }

        void TrickleDown(int index, T item)
        {
            int middleIndex = this.count >> 1;
            while (index < middleIndex)
            {
                int childIndex = (index << 1) + 1;
                T childItem = this.items[childIndex];
                int rightChildIndex = childIndex + 1;
                if (rightChildIndex < this.count
                    && this.comparer.Compare(childItem, this.items[rightChildIndex]) > 0)
                {
                    childIndex = rightChildIndex;
                    childItem = this.items[rightChildIndex];
                }
                if (this.comparer.Compare(item, childItem) <= 0)
                {
                    break;
                }
                
                this.items[index] = childItem;
                childItem.SetPriorityQueueIndex(this, index);
                
                index = childIndex;
            }
            
            this.items[index] = item;
            item.SetPriorityQueueIndex(this, index);
        }
        
        bool Contains(T node, int i) 
            => i >= 0 && i < this.count && node.Equals(this.items[i]);
        

        public IEnumerator<T> GetEnumerator()
        {
            for (int i = 0; i < this.count; i++)
            {
                yield return this.items[i];
            }
        }

        IEnumerator IEnumerable.GetEnumerator() => this.GetEnumerator();

        public void Clear()
        {
            for (int i = 0; i < this.count; i++)
            {
                this.items[i]?.SetPriorityQueueIndex(this, IndexNotInQueue);
            }
            
            this.count = 0;
            Array.Clear(this.items, 0, 0);
        }
    }
}