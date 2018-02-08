// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Common.Internal
{
    public interface IDequeue<T> : IQueue<T>
    {
        bool TryPeekLast(out T item);

        bool TryDequeueLast(out T item);
    }
}