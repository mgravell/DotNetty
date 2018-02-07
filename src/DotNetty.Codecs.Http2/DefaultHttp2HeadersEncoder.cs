// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;

    public class DefaultHttp2HeadersEncoder : Http2HeadersEncoder, Http2HeadersEncoderConfiguration
    {
        static readonly SensitivityDetector NeverSensitive = new NeverSensitiveDetector();

        readonly HpackEncoder hpackEncoder;
        readonly SensitivityDetector sensitivityDetector;
        readonly IByteBuffer tableSizeChangeOutput = Unpooled.Buffer();

        public DefaultHttp2HeadersEncoder()
            : this(NeverSensitive)
        {
        }

        public DefaultHttp2HeadersEncoder(SensitivityDetector sensitivityDetector)
            : this(sensitivityDetector, new HpackEncoder())
        {
        }

        public DefaultHttp2HeadersEncoder(SensitivityDetector sensitivityDetector, bool ignoreMaxHeaderListSize)
            : this(sensitivityDetector, new HpackEncoder(ignoreMaxHeaderListSize))
        {
        }

        public DefaultHttp2HeadersEncoder(
            SensitivityDetector sensitivityDetector,
            bool ignoreMaxHeaderListSize,
            int dynamicTableArraySizeHint)
            : this(sensitivityDetector, new HpackEncoder(ignoreMaxHeaderListSize, dynamicTableArraySizeHint))
        {
        }

        /**
         * Exposed Used for testing only! Default values used in the initial settings frame are overridden intentionally
         * for testing but violate the RFC if used outside the scope of testing.
         */
        DefaultHttp2HeadersEncoder(SensitivityDetector sensitivityDetector, HpackEncoder hpackEncoder)
        {
            Contract.Requires(sensitivityDetector != null);
            Contract.Requires(hpackEncoder != null);
            this.sensitivityDetector = sensitivityDetector;
            this.hpackEncoder = hpackEncoder;
        }

        public void encodeHeaders(int streamId, Http2Headers headers, IByteBuffer buffer)
        {
            try
            {
                // If there was a change in the table size, serialize the output from the hpackEncoder
                // resulting from that change.
                if (this.tableSizeChangeOutput.IsReadable())
                {
                    buffer.WriteBytes(this.tableSizeChangeOutput);
                    this.tableSizeChangeOutput.Clear();
                }

                this.hpackEncoder.encodeHeaders(streamId, buffer, headers, this.sensitivityDetector);
            }
            catch (Http2Exception)
            {
                throw;
            }
            catch (Exception t)
            {
                throw Http2Exception.connectionError(Http2Error.COMPRESSION_ERROR, t, "Failed encoding headers block: %s", t.Message);
            }
        }

        public void maxHeaderTableSize(long max)
        {
            this.hpackEncoder.setMaxHeaderTableSize(this.tableSizeChangeOutput, max);
        }

        public long maxHeaderTableSize()
        {
            return this.hpackEncoder.getMaxHeaderTableSize();
        }

        public void maxHeaderListSize(long max)
        {
            this.hpackEncoder.setMaxHeaderListSize(max);
        }

        public long maxHeaderListSize()
        {
            return this.hpackEncoder.getMaxHeaderListSize();
        }

        public Http2HeadersEncoderConfiguration configuration()
        {
            return this;
        }
    }
}