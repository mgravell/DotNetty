// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
   using DotNetty.Common.Utilities;

   public interface Http2Headers : IHeaders<ICharSequence, ICharSequence>
    {
        /**
           * Sets the {@link PseudoHeaderName#METHOD} header or {@code null} if there is no such header
           */
        Http2Headers method(ICharSequence value);

        /**
           * Sets the {@link PseudoHeaderName#SCHEME} header if there is no such header
           */
        Http2Headers scheme(ICharSequence value);

        /**
           * Sets the {@link PseudoHeaderName#AUTHORITY} header or {@code null} if there is no such header
           */
        Http2Headers authority(ICharSequence value);

        /**
           * Sets the {@link PseudoHeaderName#PATH} header or {@code null} if there is no such header
           */
        Http2Headers path(ICharSequence value);

        /**
           * Sets the {@link PseudoHeaderName#STATUS} header or {@code null} if there is no such header
           */
        Http2Headers status(ICharSequence value);

        /**
           * Gets the {@link PseudoHeaderName#METHOD} header or {@code null} if there is no such header
           */
        ICharSequence method();

        /**
           * Gets the {@link PseudoHeaderName#SCHEME} header or {@code null} if there is no such header
           */
        ICharSequence scheme();

        /**
           * Gets the {@link PseudoHeaderName#AUTHORITY} header or {@code null} if there is no such header
           */
        ICharSequence authority();

        /**
           * Gets the {@link PseudoHeaderName#PATH} header or {@code null} if there is no such header
           */
        ICharSequence path();

        /**
           * Gets the {@link PseudoHeaderName#STATUS} header or {@code null} if there is no such header
           */
        ICharSequence status();

        /**
           * Returns {@code true} if a header with the {@code name} and {@code value} exists, {@code false} otherwise.
           * <p>
           * If {@code caseInsensitive} is {@code true} then a case insensitive compare is done on the value.
           *
           * @param name the name of the header to find
           * @param value the value of the header to find
           * @param caseInsensitive {@code true} then a case insensitive compare is run to compare values.
           * otherwise a case sensitive compare is run to compare values.
           */
        bool contains(ICharSequence name, ICharSequence value, bool caseInsensitive);
    }
}