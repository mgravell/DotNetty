// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System.Diagnostics.Contracts;
    using DotNetty.Common.Utilities;

    class HpackHeaderField
    {
        // Section 4.1. Calculating Table Size
        // The additional 32 octets account for an estimated
        // overhead associated with the structure.
        internal  const int HEADER_ENTRY_OVERHEAD = 32;

        static long sizeOf(ICharSequence name, ICharSequence value)
        {
            return name.Count + value.Count + HEADER_ENTRY_OVERHEAD;
        }

        internal readonly ICharSequence name;
        internal readonly ICharSequence value;

        // This constructor can only be used if name and value are ISO-8859-1 encoded.
        internal HpackHeaderField(ICharSequence name, ICharSequence value)
        {
            Contract.Requires(name != null);
            Contract.Requires(value != null);
            this.name = name;
            this.value = value;
        }

        internal int size()
        {
            return this.name.Count + this.value.Count + HEADER_ENTRY_OVERHEAD;
        }

        public sealed override int GetHashCode()
        {
            // TODO(nmittler): Netty's build rules require this. Probably need a better implementation.
            return base.GetHashCode();
        }

        public sealed override bool Equals(object obj)
        {
            if (obj == this)
            {
                return true;
            }

            if (!(obj is HpackHeaderField))
            {
                return false;
            }

            HpackHeaderField other = (HpackHeaderField)obj;
            // To avoid short circuit behavior a bitwise operator is used instead of a bool operator.
            return (HpackUtil.equalsConstantTime(this.name, other.name) & HpackUtil.equalsConstantTime(this.value, other.value)) != 0;
        }

        public override string ToString()
        {
            return this.name + ": " + this.value;
        }
    }
}