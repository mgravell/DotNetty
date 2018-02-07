namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Diagnostics.Contracts;
    using DotNetty.Buffers;

    public class DefaultHttp2HeadersDecoder : Http2HeadersDecoder, Http2HeadersDecoderConfiguration
    {
        static readonly float HEADERS_COUNT_WEIGHT_NEW = 1 / 5f;
        static readonly float HEADERS_COUNT_WEIGHT_HISTORICAL = 1 - HEADERS_COUNT_WEIGHT_NEW;

        readonly HpackDecoder hpackDecoder;
        readonly bool _validateHeaders;

        /**
     * Used to calculate an exponential moving average of header sizes to get an estimate of how large the data
     * structure for storing headers should be.
     */
        float headerArraySizeAccumulator = 8;

        public DefaultHttp2HeadersDecoder() : this(true)
        {
            
        }

        public DefaultHttp2HeadersDecoder(bool validateHeaders) : this(validateHeaders, Http2CodecUtil.DEFAULT_HEADER_LIST_SIZE)
        {
            
        }

        /**
     * Create a new instance.
     * @param validateHeaders {@code true} to validate headers are valid according to the RFC.
     * @param maxHeaderListSize This is the only setting that can be configured before notifying the peer.
     *  This is because <a href="https://tools.ietf.org/html/rfc7540#section-6.5.1">SETTINGS_MAX_HEADER_LIST_SIZE</a>
     *  allows a lower than advertised limit from being enforced, and the default limit is unlimited
     *  (which is dangerous).
     */
        public DefaultHttp2HeadersDecoder(bool validateHeaders, long maxHeaderListSize)
            : this(validateHeaders, maxHeaderListSize, Http2CodecUtil.DEFAULT_INITIAL_HUFFMAN_DECODE_CAPACITY)
        {
            
        }

        /**
     * Create a new instance.
     * @param validateHeaders {@code true} to validate headers are valid according to the RFC.
     * @param maxHeaderListSize This is the only setting that can be configured before notifying the peer.
     *  This is because <a href="https://tools.ietf.org/html/rfc7540#section-6.5.1">SETTINGS_MAX_HEADER_LIST_SIZE</a>
     *  allows a lower than advertised limit from being enforced, and the default limit is unlimited
     *  (which is dangerous).
     * @param initialHuffmanDecodeCapacity Size of an intermediate buffer used during huffman decode.
     */
        public DefaultHttp2HeadersDecoder(
            bool validateHeaders,
            long maxHeaderListSize,
            int initialHuffmanDecodeCapacity) : this(validateHeaders, new HpackDecoder(maxHeaderListSize, initialHuffmanDecodeCapacity))
        {
            
        }

        /**
     * Exposed Used for testing only! Default values used in the initial settings frame are overridden intentionally
     * for testing but violate the RFC if used outside the scope of testing.
     */
        DefaultHttp2HeadersDecoder(bool validateHeaders, HpackDecoder hpackDecoder)
        {
            Contract.Requires(hpackDecoder != null);
            this.hpackDecoder = hpackDecoder;
            this._validateHeaders = validateHeaders;
        }


        public void maxHeaderTableSize(long max)
        {
            hpackDecoder.setMaxHeaderTableSize(max);
        }



        public long maxHeaderTableSize()
        {
            return hpackDecoder.getMaxHeaderTableSize();
        }


        public void maxHeaderListSize(long max, long goAwayMax)
        {
            hpackDecoder.setMaxHeaderListSize(max, goAwayMax);
        }


        public long maxHeaderListSize()
        {
            return hpackDecoder.getMaxHeaderListSize();
        }


        public long maxHeaderListSizeGoAway()
        {
            return hpackDecoder.getMaxHeaderListSizeGoAway();
        }


        public Http2HeadersDecoderConfiguration configuration()
        {
            return this;
        }


        public Http2Headers decodeHeaders(int streamId, IByteBuffer headerBlock)
        {
            try
            {
                Http2Headers headers = newHeaders();
                hpackDecoder.decode(streamId, headerBlock, headers);
                headerArraySizeAccumulator = HEADERS_COUNT_WEIGHT_NEW * headers.Size +
                    HEADERS_COUNT_WEIGHT_HISTORICAL * headerArraySizeAccumulator;
                return headers;
            }
            catch (Http2Exception)
            {
                throw;
            }
            catch (Exception e)
            {
                // Default handler for any other types of errors that may have occurred. For example,
                // the the Header builder throws IllegalArgumentException if the key or value was invalid
                // for any reason (e.g. the key was an invalid pseudo-header).
                throw Http2Exception.connectionError(Http2Error.COMPRESSION_ERROR, e, e.Message);
            }
        }

        /**
         * A weighted moving average estimating how many headers are expected during the decode process.
         * @return an estimate of how many headers are expected during the decode process.
         */
        protected int numberOfHeadersGuess()
        {
            return (int)headerArraySizeAccumulator;
        }

        /**
         * Determines if the headers should be validated as a result of the decode operation.
         * @return {@code true} if the headers should be validated as a result of the decode operation.
         */
        protected bool validateHeaders()
        {
            return _validateHeaders;
        }

        /**
         * Create a new {@link Http2Headers} object which will store the results of the decode operation.
         * @return a new {@link Http2Headers} object which will store the results of the decode operation.
         */
        protected Http2Headers newHeaders()
        {
            return new DefaultHttp2Headers(_validateHeaders, (int)headerArraySizeAccumulator);
        }
    }
}
