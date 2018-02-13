// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.Collections;
    using System.Collections.Generic;

    /**
     * Provides the ability to handle multiple stream exceptions with one throw statement.
     */
    public sealed class CompositeStreamException : Http2Exception, IEnumerable<StreamException>
    {
        readonly List<StreamException> exceptions;

        public CompositeStreamException(Http2Error error, int initialCapacity)
            : base(error, ShutdownHint.NO_SHUTDOWN)
        {
            this.exceptions = new List<StreamException>(initialCapacity);
        }

        public void add(StreamException e)
        {
            this.exceptions.Add(e);
        }

        public IEnumerator<StreamException> GetEnumerator()
        {
            return this.exceptions.GetEnumerator();
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }
    }
}