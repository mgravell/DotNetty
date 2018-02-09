// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections.Generic;
    using System.Diagnostics.Contracts;
    using System.Threading.Tasks;
    using DotNetty.Common.Concurrency;

    /**
     * Provides the ability to associate the outcome of multiple {@link IPromise}
     * objects into a single {@link IPromise} object.
     */
    sealed class SimplePromiseAggregator : IPromise
    {
        readonly IPromise promise;
        int expectedCount;
        int doneCount;
        bool doneAllocating;

        Exception lastFailure;
        IEnumerable<Exception> lastFailures;

        public SimplePromiseAggregator(IPromise promise)
        {
            Contract.Assert(promise != null && !promise.Task.IsCompleted);
            this.promise = promise;
        }

        public Task Task => this.promise.Task;

        public bool IsVoid => false;

        /**
         * Allocate a new promise which will be used to aggregate the overall success of this promise aggregator.
         * @return A new promise which will be aggregated.
         * {@code null} if {@link #doneAllocatingPromises()} was previously called.
         */
        public IPromise newPromise()
        {
            Contract.Assert(!this.doneAllocating, "Done allocating. No more promises can be allocated.");
            ++this.expectedCount;
            return this;
        }

        /**
         * Signify that no more {@link #newPromise()} allocations will be made.
         * The aggregation can not be successful until this method is called.
         * @return The promise that is the aggregation of all promises allocated with {@link #newPromise()}.
         */
        public void doneAllocatingPromises()
        {
            if (!this.doneAllocating)
            {
                this.doneAllocating = true;
                if (this.doneCount == this.expectedCount || this.expectedCount == 0)
                {
                    this.setPromise();
                }
            }
        }

        public bool TrySetException(Exception cause)
        {
            if (this.allowFailure())
            {
                ++this.doneCount;
                this.lastFailure = cause;
                if (this.allPromisesDone())
                {
                    return this.tryPromise();
                }

                // TODO: We break the interface a bit here.
                // Multiple failure events can be processed without issue because this is an aggregation.
                return true;
            }

            return false;
        }

        public bool TrySetException(IEnumerable<Exception> ex)
        {
            if (this.allowFailure())
            {
                ++this.doneCount;
                this.lastFailures = ex;
                if (this.allPromisesDone())
                {
                    return this.tryPromise();
                }

                // TODO: We break the interface a bit here.
                // Multiple failure events can be processed without issue because this is an aggregation.
                return true;
            }

            return false;
        }

        /**
         * Fail this object if it has not already been failed.
         * <p>
         * This method will NOT throw an {@link IllegalStateException} if called multiple times
         * because that may be expected.
         */
        public void SetException(Exception cause)
        {
            if (this.allowFailure())
            {
                ++this.doneCount;
                this.lastFailure = cause;
                if (this.allPromisesDone())
                {
                    this.setPromise();
                }
            }
        }

        public bool TrySetCanceled()
        {
            throw new NotImplementedException();
        }

        public void SetCanceled()
        {
            throw new NotImplementedException();
        }

        public bool SetUncancellable() => true;

        public void Complete()
        {
            if (this.awaitingPromises())
            {
                ++this.doneCount;
                if (this.allPromisesDone())
                {
                    this.setPromise();
                }
            }
        }

        public bool TryComplete()
        {
            if (this.awaitingPromises())
            {
                ++this.doneCount;
                if (this.allPromisesDone())
                {
                    return this.tryPromise();
                }

                // TODO: We break the interface a bit here.
                // Multiple success events can be processed without issue because this is an aggregation.
                return true;
            }

            return false;
        }

        bool allowFailure()
        {
            return this.awaitingPromises() || this.expectedCount == 0;
        }

        bool awaitingPromises()
        {
            return this.doneCount < this.expectedCount;
        }

        bool allPromisesDone()
        {
            return this.doneCount == this.expectedCount && this.doneAllocating;
        }

        void setPromise()
        {
            if (this.lastFailure != null)
            {
                this.promise.SetException(this.lastFailure);
            }
            else
            {
                this.promise.Complete();
            }
        }

        bool tryPromise()
        {
            if (this.lastFailure != null)
                return this.promise.TrySetException(this.lastFailure);
            else if (this.lastFailures != null)
                return this.promise.TrySetException(this.lastFailures);
            else
                return this.promise.TryComplete();
        }
    }
}