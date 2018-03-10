﻿// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common
{
    using System.Collections.Generic;

    public sealed class ThreadLocalObjectList : List<object>
    {
        const int DefaultInitialCapacity = 8;

        static readonly ThreadLocalPool<ThreadLocalObjectList> Pool = new ThreadLocalPool<ThreadLocalObjectList>(handle => new ThreadLocalObjectList(handle));

        readonly ThreadLocalPool.Handle returnHandle;

        protected ThreadLocalObjectList(ThreadLocalPool.Handle returnHandle)
        {
            this.returnHandle = returnHandle;
        }

        public static ThreadLocalObjectList NewInstance() => NewInstance(DefaultInitialCapacity);

        public static ThreadLocalObjectList NewInstance(int minCapacity)
        {
            ThreadLocalObjectList ret = Pool.Take();
            if (ret.Capacity < minCapacity)
            {
                ret.Capacity = minCapacity;
            }
            return ret;
        }

        public void Return()
        {
            this.Clear();
            this.returnHandle.Release(this);
        }
    }
    
    public class ThreadLocalObjectList<T> : List<T>
    {
        const int DefaultInitialCapacity = 8;

        static readonly ThreadLocalPool<ThreadLocalObjectList<T>> Pool = new ThreadLocalPool<ThreadLocalObjectList<T>>(handle => new ThreadLocalObjectList<T>(handle));

        readonly ThreadLocalPool.Handle returnHandle;

        protected ThreadLocalObjectList(ThreadLocalPool.Handle returnHandle)
        {
            this.returnHandle = returnHandle;
        }

        public static ThreadLocalObjectList<T> NewInstance() => NewInstance(DefaultInitialCapacity);

        public static ThreadLocalObjectList<T> NewInstance(int minCapacity)
        {
            ThreadLocalObjectList<T> ret = Pool.Take();
            if (ret.Capacity < minCapacity)
            {
                ret.Capacity = minCapacity;
            }
            return ret;
        }

        public void Return()
        {
            this.Clear();
            this.returnHandle.Release(this);
        }
    }
}