// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Codecs.Http;
    using DotNetty.Common.Utilities;

    public class DefaultHttp2Headers : DefaultHeaders<ICharSequence, ICharSequence>, Http2Headers
    {
        class Http2NameValidatorProcessor : IByteProcessor
        {
            public bool Process(byte value) => !AsciiString.IsUpperCase(value);
        }

        class Http2NameValidator : INameValidator<ICharSequence>
        {
            public void ValidateName(ICharSequence name)
            {
                if (name == null || name.Count == 0)
                {
                    Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "empty headers are not allowed [{0}]", name);
                }

                if (name is AsciiString asciiString)
                {
                    int index = 0;
                    try
                    {
                        index = asciiString.ForEachByte(HTTP2_NAME_VALIDATOR_PROCESSOR);
                    }
                    catch (Http2Exception)
                    {
                        throw;
                    }
                    catch (Exception t)
                    {
                        Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, t, "unexpected error. invalid header name [{0}]", name);
                    }

                    if (index != -1)
                    {
                        Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "invalid header name [{0}]", name);
                    }
                }
                else
                {
                    for (int i = 0; i < name.Count; ++i)
                    {
                        if (AsciiString.IsUpperCase(name[i]))
                        {
                            Http2Exception.connectionError(Http2Error.PROTOCOL_ERROR, "invalid header name [{0}]", name);
                        }
                    }
                }
            }
        }

        static readonly IByteProcessor HTTP2_NAME_VALIDATOR_PROCESSOR = new Http2NameValidatorProcessor();

        static readonly INameValidator<ICharSequence> HTTP2_NAME_VALIDATOR = new Http2NameValidator();

        HeaderEntry<ICharSequence, ICharSequence> firstNonPseudo = head;

        /**
     * Create a new instance.
     * <p>
     * Header names will be validated according to
     * <a href="https://tools.ietf.org/html/rfc7540">rfc7540</a>.
     */
        public DefaultHttp2Headers()
            : this(true)
        {
        }

        /**
     * Create a new instance.
     * @param validate {@code true} to validate header names according to
     * <a href="https://tools.ietf.org/html/rfc7540">rfc7540</a>. {@code false} to not validate header names.
     */
        public DefaultHttp2Headers(bool validate)
            : base(AsciiString.CaseSensitiveHasher, CharSequenceValueConverter.Instance, validate ? HTTP2_NAME_VALIDATOR : DefaultHttpHeaders.NotNullValidator)
        {
            // Case sensitive compare is used because it is cheaper, and header validation can be used to catch invalid
            // headers.
        }

        /**
     * Create a new instance.
     * @param validate {@code true} to validate header names according to
     * <a href="https://tools.ietf.org/html/rfc7540">rfc7540</a>. {@code false} to not validate header names.
     * @param arraySizeHint A hint as to how large the hash data structure should be.
     * The next positive power of two will be used. An upper bound may be enforced.
     */
        public DefaultHttp2Headers(bool validate, int arraySizeHint)
            : base(AsciiString.CaseSensitiveHasher, CharSequenceValueConverter.Instance, validate ? HTTP2_NAME_VALIDATOR : DefaultHttpHeaders.NotNullValidator, arraySizeHint)
        {
            // Case sensitive compare is used because it is cheaper, and header validation can be used to catch invalid
            // headers.
        }

        public override IHeaders<ICharSequence, ICharSequence> Clear()
        {
            this.firstNonPseudo = this.head;
            return base.Clear();
        }

        public override bool Equals(object o)
        {
            return o is Http2Headers headers && this.Equals(headers, AsciiString.CaseSensitiveHasher);
        }

        public override int GetHashCode()
        {
            return this.HashCode(AsciiString.CaseSensitiveHasher);
        }

        public Http2Headers method(ICharSequence value)
        {
            this.Set(PseudoHeaderName.METHOD.value(), value);
            return this;
        }

        public Http2Headers scheme(ICharSequence value)
        {
            this.Set(PseudoHeaderName.SCHEME.value(), value);
            return this;
        }

        public Http2Headers authority(ICharSequence value)
        {
            this.Set(PseudoHeaderName.AUTHORITY.value(), value);
            return this;
        }

        public Http2Headers path(ICharSequence value)
        {
            this.Set(PseudoHeaderName.PATH.value(), value);
            return this;
        }

        public Http2Headers status(ICharSequence value)
        {
            this.Set(PseudoHeaderName.STATUS.value(), value);
            return this;
        }

        public ICharSequence method()
        {
            return this.Get(PseudoHeaderName.METHOD.value());
        }

        public ICharSequence scheme()
        {
            return this.Get(PseudoHeaderName.SCHEME.value());
        }

        public ICharSequence authority()
        {
            return this.Get(PseudoHeaderName.AUTHORITY.value());
        }

        public ICharSequence path()
        {
            return this.Get(PseudoHeaderName.PATH.value());
        }

        public ICharSequence status()
        {
            return this.Get(PseudoHeaderName.STATUS.value());
        }

        public bool contains(ICharSequence name, ICharSequence value, bool caseInsensitive)
        {
            return this.Contains(name, value, caseInsensitive ? AsciiString.CaseInsensitiveHasher : AsciiString.CaseSensitiveHasher);
        }

        protected HeaderEntry<ICharSequence, ICharSequence> newHeaderEntry(
            int h,
            ICharSequence name,
            ICharSequence value,
            HeaderEntry<ICharSequence, ICharSequence> next)
        {
            return new Http2HeaderEntry(this, h, name, value, next);
        }

        sealed class Http2HeaderEntry : HeaderEntry<ICharSequence, ICharSequence>
        {
            readonly DefaultHttp2Headers headers;

            internal Http2HeaderEntry(DefaultHttp2Headers headers, int hash, ICharSequence key, ICharSequence value, HeaderEntry<ICharSequence, ICharSequence> next)
                : base(hash, key)
            {
                this.headers = headers;
                this.value = value;
                this.Next = next;

                // Make sure the pseudo headers fields are first in iteration order
                if (key.Count != 0 && key[0] == ':')
                {
                    this.After = this.headers.firstNonPseudo;
                    this.Before = this.headers.firstNonPseudo.Before;
                }
                else
                {
                    this.After = this.headers.head;
                    this.Before = this.headers.head.Before;
                    if (this.headers.firstNonPseudo == this.headers.head)
                    {
                        this.headers.firstNonPseudo = this;
                    }
                }

                this.PointNeighborsToThis();
            }

            public override void Remove()
            {
                if (this == this.headers.firstNonPseudo)
                {
                    this.headers.firstNonPseudo = this.headers.firstNonPseudo.After;
                }

                base.Remove();
            }
        }
    }
}