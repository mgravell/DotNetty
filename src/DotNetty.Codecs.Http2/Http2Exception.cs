// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;

    /**
     * Exception thrown when an HTTP/2 error was encountered.
     */
    public class Http2Exception : Exception
    {
        static readonly long serialVersionUID = -6941186345430164209L;
        readonly Http2Error _error;
        readonly ShutdownHint _shutdownHint;

        public Http2Exception(Http2Error error)
            : this(error, ShutdownHint.HARD_SHUTDOWN)
        {
        }

        public Http2Exception(Http2Error error, ShutdownHint shutdownHint)
        {
            this._error = error;
            this._shutdownHint = shutdownHint;
        }

        public Http2Exception(Http2Error error, string message)
            : this(error, message, ShutdownHint.HARD_SHUTDOWN)
        {
        }

        public Http2Exception(Http2Error error, string message, ShutdownHint shutdownHint)
            : base(message)
        {
            this._error = error;
            this._shutdownHint = shutdownHint;
        }

        public Http2Exception(Http2Error error, string message, Exception cause)
            : this(error, message, cause, ShutdownHint.HARD_SHUTDOWN)
        {
        }

        public Http2Exception(Http2Error error, string message, Exception cause, ShutdownHint shutdownHint)
            : base(message, cause)
        {
            this._error = error;
            this._shutdownHint = shutdownHint;
        }

        public Http2Error error()
        {
            return this._error;
        }

        /**
         * Provide a hint as to what type of shutdown should be executed. Note this hint may be ignored.
         */
        public ShutdownHint shutdownHint()
        {
            return this._shutdownHint;
        }

        /**
         * Use if an error has occurred which can not be isolated to a single stream, but instead applies
         * to the entire connection.
         * @param error The type of error as defined by the HTTP/2 specification.
         * @param fmt string with the content and format for the additional debug data.
         * @param args Objects which fit into the format defined by {@code fmt}.
         * @return An exception which can be translated into a HTTP/2 error.
         */
        public static Http2Exception connectionError(Http2Error error, string fmt, params object[] args)
        {
            return new Http2Exception(error, string.Format(fmt, args));
        }

        /**
         * Use if an error has occurred which can not be isolated to a single stream, but instead applies
         * to the entire connection.
         * @param error The type of error as defined by the HTTP/2 specification.
         * @param cause The object which caused the error.
         * @param fmt string with the content and format for the additional debug data.
         * @param args Objects which fit into the format defined by {@code fmt}.
         * @return An exception which can be translated into a HTTP/2 error.
         */
        public static Http2Exception connectionError(Http2Error error, Exception cause, string fmt, params object[] args)
        {
            return new Http2Exception(error, string.Format(fmt, args), cause);
        }

        /**
         * Use if an error has occurred which can not be isolated to a single stream, but instead applies
         * to the entire connection.
         * @param error The type of error as defined by the HTTP/2 specification.
         * @param fmt string with the content and format for the additional debug data.
         * @param args Objects which fit into the format defined by {@code fmt}.
         * @return An exception which can be translated into a HTTP/2 error.
         */
        public static Http2Exception closedStreamError(Http2Error error, string fmt, params object[] args)
        {
            return new ClosedStreamCreationException(error, string.Format(fmt, args));
        }

        /**
         * Use if an error which can be isolated to a single stream has occurred.  If the {@code id} is not
         * {@link Http2CodecUtil#Http2CodecUtil.CONNECTION_STREAM_ID} then a {@link Http2Exception.StreamException} will be returned.
         * Otherwise the error is considered a connection error and a {@link Http2Exception} is returned.
         * @param id The stream id for which the error is isolated to.
         * @param error The type of error as defined by the HTTP/2 specification.
         * @param fmt string with the content and format for the additional debug data.
         * @param args Objects which fit into the format defined by {@code fmt}.
         * @return If the {@code id} is not
         * {@link Http2CodecUtil#Http2CodecUtil.CONNECTION_STREAM_ID} then a {@link Http2Exception.StreamException} will be returned.
         * Otherwise the error is considered a connection error and a {@link Http2Exception} is returned.
         */
        public static Http2Exception streamError(int id, Http2Error error, string fmt, params object[] args)
        {
            return Http2CodecUtil.CONNECTION_STREAM_ID == id ? connectionError(error, fmt, args) : new StreamException(id, error, string.Format(fmt, args));
        }

        /**
         * Use if an error which can be isolated to a single stream has occurred.  If the {@code id} is not
         * {@link Http2CodecUtil#Http2CodecUtil.CONNECTION_STREAM_ID} then a {@link Http2Exception.StreamException} will be returned.
         * Otherwise the error is considered a connection error and a {@link Http2Exception} is returned.
         * @param id The stream id for which the error is isolated to.
         * @param error The type of error as defined by the HTTP/2 specification.
         * @param cause The object which caused the error.
         * @param fmt string with the content and format for the additional debug data.
         * @param args Objects which fit into the format defined by {@code fmt}.
         * @return If the {@code id} is not
         * {@link Http2CodecUtil#Http2CodecUtil.CONNECTION_STREAM_ID} then a {@link Http2Exception.StreamException} will be returned.
         * Otherwise the error is considered a connection error and a {@link Http2Exception} is returned.
         */
        public static Http2Exception streamError(
            int id,
            Http2Error error,
            Exception cause,
            string fmt,
            params object[] args)
        {
            return Http2CodecUtil.CONNECTION_STREAM_ID == id ? connectionError(error, cause, fmt, args) : new StreamException(id, error, string.Format(fmt, args), cause);
        }

        /**
         * A specific stream error resulting from failing to decode headers that exceeds the max header size list.
         * If the {@code id} is not {@link Http2CodecUtil#Http2CodecUtil.CONNECTION_STREAM_ID} then a
         * {@link Http2Exception.StreamException} will be returned. Otherwise the error is considered a
         * connection error and a {@link Http2Exception} is returned.
         * @param id The stream id for which the error is isolated to.
         * @param error The type of error as defined by the HTTP/2 specification.
         * @param onDecode Whether this error was caught while decoding headers
         * @param fmt string with the content and format for the additional debug data.
         * @param args Objects which fit into the format defined by {@code fmt}.
         * @return If the {@code id} is not
         * {@link Http2CodecUtil#Http2CodecUtil.CONNECTION_STREAM_ID} then a {@link HeaderListSizeException}
         * will be returned. Otherwise the error is considered a connection error and a {@link Http2Exception} is
         * returned.
         */
        public static Http2Exception headerListSizeError(
            int id,
            Http2Error error,
            bool onDecode,
            string fmt,
            params object[] args)
        {
            return Http2CodecUtil.CONNECTION_STREAM_ID == id ? connectionError(error, fmt, args) : new HeaderListSizeException(id, error, string.Format(fmt, args), onDecode);
        }

        /**
         * Check if an exception is isolated to a single stream or the entire connection.
         * @param e The exception to check.
         * @return {@code true} if {@code e} is an instance of {@link Http2Exception.StreamException}.
         * {@code false} otherwise.
         */
        public static bool isStreamError(Http2Exception e)
        {
            return e is StreamException;
        }

        /**
         * Get the stream id associated with an exception.
         * @param e The exception to get the stream id for.
         * @return {@link Http2CodecUtil#Http2CodecUtil.CONNECTION_STREAM_ID} if {@code e} is a connection error.
         * Otherwise the stream id associated with the stream error.
         */
        public static int streamId(Http2Exception e)
        {
            return isStreamError(e) ? ((StreamException)e).streamId() : Http2CodecUtil.CONNECTION_STREAM_ID;
        }
    }
}