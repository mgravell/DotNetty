// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    sealed class HpackHuffmanEncoder
    {
        readonly int[] codes;
        readonly byte[] lengths;
        readonly EncodedLengthProcessor encodedLengthProcessor;
        readonly EncodeProcessor encodeProcessor;

        internal HpackHuffmanEncoder()
            : this(HpackUtil.HUFFMAN_CODES, HpackUtil.HUFFMAN_CODE_LENGTHS)
        {
        }

        /**
         * Creates a new Huffman encoder with the specified Huffman coding.
         *
         * @param codes the Huffman codes indexed by symbol
         * @param lengths the length of each Huffman code
         */
        internal HpackHuffmanEncoder(int[] codes, byte[] lengths)
        {
            this.encodedLengthProcessor = new EncodedLengthProcessor(this);
            this.encodeProcessor = new EncodeProcessor(this);
            this.codes = codes;
            this.lengths = lengths;
        }

        /**
     * Compresses the input string literal using the Huffman coding.
     *
     * @param ouput the output stream for the compressed data
     * @param data the string literal to be Huffman encoded
     */
        public void encode(IByteBuffer ouput, ICharSequence data)
        {
            Contract.Requires(ouput != null);
            if (data is AsciiString str)
            {
                try
                {
                    this.encodeProcessor.ouput = ouput;
                    str.ForEachByte(this.encodeProcessor);
                }
                finally
                {
                    this.encodeProcessor.end();
                }
            }
            else
            {
                this.encodeSlowPath(ouput, data);
            }
        }

        void encodeSlowPath(IByteBuffer ouput, ICharSequence data)
        {
            long current = 0;
            int n = 0;

            for (int i = 0; i < data.Count; i++)
            {
                int b = data[i] & 0xFF;
                int code = this.codes[b];
                int nbits = this.lengths[b];

                current <<= nbits;
                current |= code;
                n += nbits;

                while (n >= 8)
                {
                    n -= 8;
                    ouput.WriteByte((int)(current >> n));
                }
            }

            if (n > 0)
            {
                current <<= 8 - n;
                current |= 0xFF >> n; // this should be EOS symbol
                ouput.WriteByte((int)current);
            }
        }

        /**
     * Returns the number of bytes required to Huffman encode the input string literal.
     *
     * @param data the string literal to be Huffman encoded
     * @return the number of bytes required to Huffman encode {@code data}
     */
        internal int getEncodedLength(ICharSequence data)
        {
            if (data is AsciiString str)
            {
                try
                {
                    this.encodedLengthProcessor.reset();
                    str.ForEachByte(this.encodedLengthProcessor);
                    return this.encodedLengthProcessor.Count;
                }
                catch (Exception e)
                {
                    throw;
                    ;
                    return -1;
                }
            }
            else
            {
                return this.getEncodedLengthSlowPath(data);
            }
        }

        int getEncodedLengthSlowPath(ICharSequence data)
        {
            long len = 0;
            for (int i = 0; i < data.Count; i++)
            {
                len += this.lengths[data[i] & 0xFF];
            }

            return (int)((len + 7) >> 3);
        }

        sealed class EncodeProcessor : IByteProcessor
        {
            readonly HpackHuffmanEncoder encoder;
            internal IByteBuffer ouput;
            long current;
            int n;

            public EncodeProcessor(HpackHuffmanEncoder encoder)
            {
                this.encoder = encoder;
            }

            public bool Process(byte value)
            {
                int b = value & 0xFF;
                int nbits = this.encoder.lengths[b];

                this.current <<= nbits;
                this.current |= this.encoder.codes[b];
                this.n += nbits;

                while (this.n >= 8)
                {
                    this.n -= 8;
                    this.ouput.WriteByte((int)(this.current >> this.n));
                }

                return true;
            }

            internal void end()
            {
                try
                {
                    if (this.n > 0)
                    {
                        this.current <<= 8 - this.n;
                        this.current |= 0xFF >> this.n; // this should be EOS symbol
                        this.ouput.WriteByte((int)this.current);
                    }
                }
                finally
                {
                    this.ouput = null;
                    this.current = 0;
                    this.n = 0;
                }
            }
        }

        sealed class EncodedLengthProcessor : IByteProcessor
        {
            readonly HpackHuffmanEncoder encoder;
            long len;

            public EncodedLengthProcessor(HpackHuffmanEncoder encoder)
            {
                this.encoder = encoder;
            }

            public bool Process(byte value)
            {
                this.len += this.encoder.lengths[value & 0xFF];
                return true;
            }

            internal void reset()
            {
                this.len = 0;
            }

            internal int Count => (int)((this.len + 7) >> 3);
        }
    }
}