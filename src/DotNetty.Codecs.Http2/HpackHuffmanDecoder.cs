// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;
    using DotNetty.Common.Utilities;

    sealed class HpackHuffmanDecoder
    {
        static readonly Http2Exception EOS_DECODED = Http2Exception.connectionError(Http2Error.COMPRESSION_ERROR, "HPACK - EOS Decoded");

        static readonly Http2Exception INVALID_PADDING = Http2Exception.connectionError(Http2Error.COMPRESSION_ERROR, "HPACK - Invalid Padding");

        static readonly Node ROOT = buildTree(HpackUtil.HUFFMAN_CODES, HpackUtil.HUFFMAN_CODE_LENGTHS);

        readonly DecoderProcessor processor;

        internal HpackHuffmanDecoder(int initialCapacity)
        {
            this.processor = new DecoderProcessor(initialCapacity);
        }

        /**
     * Decompresses the given Huffman coded string literal.
     *
     * @param buf the string literal to be decoded
     * @return the output stream for the compressed data
     * @ EOS Decoded
     */
        public AsciiString decode(IByteBuffer buf, int length)
        {
            this.processor.reset();
            buf.ForEachByte(buf.ReaderIndex, length, this.processor);
            buf.SkipBytes(length);
            return this.processor.end();
        }

        sealed class Node
        {
            internal readonly int symbol; // terminal nodes have a symbol
            internal readonly int bits; // number of bits matched by the node
            internal readonly Node[] children; // internal nodes have children

            /**
             * Construct an internal node
             */
            internal Node()
            {
                this.symbol = 0;
                this.bits = 8;
                this.children = new Node[256];
            }

            /**
             * Construct a terminal node
             *
             * @param symbol the symbol the node represents
             * @param bits the number of bits matched by this node
             */
            internal Node(int symbol, int bits)
            {
                Contract.Assert(bits > 0 && bits <= 8);
                this.symbol = symbol;
                this.bits = bits;
                this.children = null;
            }

            internal bool isTerminal()
            {
                return this.children == null;
            }
        }

        static Node buildTree(int[] codes, byte[] lengths)
        {
            Node root = new Node();
            for (int i = 0; i < codes.Length; i++)
            {
                insert(root, i, codes[i], lengths[i]);
            }

            return root;
        }

        static void insert(Node root, int symbol, int code, byte length)
        {
            // traverse tree using the most significant bytes of code
            Node current = root;
            while (length > 8)
            {
                if (current.isTerminal())
                {
                    throw new InvalidOperationException("invalid Huffman code: prefix not unique");
                }

                length -= 8;
                int i = (code >> length) & 0xFF;
                if (current.children[i] == null)
                {
                    current.children[i] = new Node();
                }

                current = current.children[i];
            }

            Node terminal = new Node(symbol, length);
            int shift = 8 - length;
            int start = (code << shift) & 0xFF;
            int end = 1 << shift;
            for (int i = start; i < start + end; i++)
            {
                current.children[i] = terminal;
            }
        }

        sealed class DecoderProcessor : IByteProcessor
        {
            readonly int initialCapacity;
            byte[] bytes;
            int index;
            Node node;
            int current;
            int currentBits;
            int symbolBits;

            internal DecoderProcessor(int initialCapacity)
            {
                Contract.Requires(initialCapacity > 0);
                this.initialCapacity = initialCapacity;
            }

            internal void reset()
            {
                this.node = ROOT;
                this.current = 0;
                this.currentBits = 0;
                this.symbolBits = 0;
                this.bytes = new byte[this.initialCapacity];
                this.index = 0;
            }

            /*
                 * The idea here is to consume whole bytes at a time rather than individual bits. node
                 * represents the Huffman tree, with all bit patterns denormalized as 256 children. Each
                 * child represents the last 8 bits of the huffman code. The parents of each child each
                 * represent the successive 8 bit chunks that lead up to the last most part. 8 bit bytes
                 * from buf are used to traverse these tree until a terminal node is found.
                 *
                 * current is a bit buffer. The low order bits represent how much of the huffman code has
                 * not been used to traverse the tree. Thus, the high order bits are just garbage.
                 * currentBits represents how many of the low order bits of current are actually valid.
                 * currentBits will vary between 0 and 15.
                 *
                 * symbolBits is the number of bits of the the symbol being decoded, *including* all those
                 * of the parent nodes. symbolBits tells how far down the tree we are. For example, when
                 * decoding the invalid sequence {0xff, 0xff}, currentBits will be 0, but symbolBits will be
                 * 16. This is used to know if buf ended early (before consuming a whole symbol) or if
                 * there is too much padding.
                 */
            public bool Process(byte value)
            {
                this.current = (this.current << 8) | (value & 0xFF);
                this.currentBits += 8;
                this.symbolBits += 8;
                // While there are unconsumed bits in current, keep consuming symbols.
                do
                {
                    this.node = this.node.children[(this.current >> (this.currentBits - 8)) & 0xFF];
                    this.currentBits -= this.node.bits;
                    if (this.node.isTerminal())
                    {
                        if (this.node.symbol == HpackUtil.HUFFMAN_EOS)
                        {
                            throw EOS_DECODED;
                        }

                        this.append(this.node.symbol);
                        this.node = ROOT;
                        // Upon consuming a whole symbol, reset the symbol bits to the number of bits
                        // left over in the byte.
                        this.symbolBits = this.currentBits;
                    }
                }
                while (this.currentBits >= 8);

                return true;
            }

            internal AsciiString end()
            {
                /*
                         * We have consumed all the bytes in buf, but haven't consumed all the symbols. We may be on
                         * a partial symbol, so consume until there is nothing left. This will loop at most 2 times.
                         */
                while (this.currentBits > 0)
                {
                    this.node = this.node.children[(this.current << (8 - this.currentBits)) & 0xFF];
                    if (this.node.isTerminal() && this.node.bits <= this.currentBits)
                    {
                        if (this.node.symbol == HpackUtil.HUFFMAN_EOS)
                        {
                            throw EOS_DECODED;
                        }

                        this.currentBits -= this.node.bits;
                        this.append(this.node.symbol);
                        this.node = ROOT;
                        this.symbolBits = this.currentBits;
                    }
                    else
                    {
                        break;
                    }
                }

                // Section 5.2. String Literal Representation
                // A padding strictly longer than 7 bits MUST be treated as a decoding error.
                // Padding not corresponding to the most significant bits of the code
                // for the EOS symbol (0xFF) MUST be treated as a decoding error.
                int mask = (1 << this.symbolBits) - 1;
                if (this.symbolBits > 7 || (this.current & mask) != mask)
                {
                    throw INVALID_PADDING;
                }

                return new AsciiString(this.bytes, 0, this.index, false);
            }

            void append(int i)
            {
                if (this.bytes.Length == this.index)
                {
                    // Choose an expanding strategy depending on how big the buffer already is.
                    // 1024 was choosen as a good guess and we may be able to investigate more if there are better choices.
                    // See also https://github.com/netty/netty/issues/6846
                    int newLength = this.bytes.Length >= 1024 ? this.bytes.Length + this.initialCapacity : this.bytes.Length << 1;
                    byte[] newBytes = new byte[newLength];
                    Array.Copy(this.bytes, 0, newBytes, 0, this.bytes.Length);
                    this.bytes = newBytes;
                }

                this.bytes[this.index++] = (byte)i;
            }
        }
    }
}