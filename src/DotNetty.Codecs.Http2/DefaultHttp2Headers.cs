namespace DotNetty.Codecs.Http2
{
    using System;
    using DotNetty.Common.Internal;
    using DotNetty.Common.Utilities;

    public class DefaultHttp2Headers : DefaultHeaders<ICharSequence, ICharSequence>, Http2Headers {
        private static final ByteProcessor HTTP2_NAME_VALIDATOR_PROCESSOR = new ByteProcessor() {
            @Override
            public bool process(byte value) throws Exception {
                return !isUpperCase(value);
            }
        };
        static final NameValidator<ICharSequence> HTTP2_NAME_VALIDATOR = new NameValidator<ICharSequence>() {
            @Override
            public void validateName(ICharSequence name) {
                if (name == null || name.length() == 0) {
                    PlatformDependent.throwException(connectionError(PROTOCOL_ERROR,
                        "empty headers are not allowed [%s]", name));
                }
                if (name instanceof AsciiString) {
                    final int index;
                    try {
                        index = ((AsciiString) name).forEachByte(HTTP2_NAME_VALIDATOR_PROCESSOR);
                    } catch (Http2Exception e) {
                        PlatformDependent.throwException(e);
                        return;
                    } catch (Throwable t) {
                        PlatformDependent.throwException(connectionError(PROTOCOL_ERROR, t,
                            "unexpected error. invalid header name [%s]", name));
                        return;
                    }

                    if (index != -1) {
                        PlatformDependent.throwException(connectionError(PROTOCOL_ERROR,
                            "invalid header name [%s]", name));
                    }
                } else {
                    for (int i = 0; i < name.length(); ++i) {
                        if (isUpperCase(name.charAt(i))) {
                            PlatformDependent.throwException(connectionError(PROTOCOL_ERROR,
                                "invalid header name [%s]", name));
                        }
                    }
                }
            }
        };

        private HeaderEntry<ICharSequence, ICharSequence> firstNonPseudo = head;

        /**
     * Create a new instance.
     * <p>
     * Header names will be validated according to
     * <a href="https://tools.ietf.org/html/rfc7540">rfc7540</a>.
     */
        public DefaultHttp2Headers() {
            this(true);
        }

        /**
     * Create a new instance.
     * @param validate {@code true} to validate header names according to
     * <a href="https://tools.ietf.org/html/rfc7540">rfc7540</a>. {@code false} to not validate header names.
     */
        @SuppressWarnings("unchecked")
        public DefaultHttp2Headers(bool validate) {
            // Case sensitive compare is used because it is cheaper, and header validation can be used to catch invalid
            // headers.
            super(CASE_SENSITIVE_HASHER,
                ICharSequenceValueConverter.INSTANCE,
                validate ? HTTP2_NAME_VALIDATOR : NameValidator.NOT_NULL);
        }

        /**
     * Create a new instance.
     * @param validate {@code true} to validate header names according to
     * <a href="https://tools.ietf.org/html/rfc7540">rfc7540</a>. {@code false} to not validate header names.
     * @param arraySizeHint A hint as to how large the hash data structure should be.
     * The next positive power of two will be used. An upper bound may be enforced.
     */
        @SuppressWarnings("unchecked")
        public DefaultHttp2Headers(bool validate, int arraySizeHint) {
            // Case sensitive compare is used because it is cheaper, and header validation can be used to catch invalid
            // headers.
            super(CASE_SENSITIVE_HASHER,
                ICharSequenceValueConverter.INSTANCE,
                validate ? HTTP2_NAME_VALIDATOR : NameValidator.NOT_NULL,
                arraySizeHint);
        }

        public override IHeaders<ICharSequence, ICharSequence> Clear() {
            this.firstNonPseudo = head;
            return base.Clear();
        }

        @Override
        public bool equals(Object o) {
            return o instanceof Http2Headers && equals((Http2Headers) o, CASE_SENSITIVE_HASHER);
        }

        @Override
        public int hashCode() {
            return hashCode(CASE_SENSITIVE_HASHER);
        }

        @Override
        public Http2Headers method(ICharSequence value) {
            set(PseudoHeaderName.METHOD.value(), value);
            return this;
        }

        @Override
        public Http2Headers scheme(ICharSequence value) {
            set(PseudoHeaderName.SCHEME.value(), value);
            return this;
        }

        @Override
        public Http2Headers authority(ICharSequence value) {
            set(PseudoHeaderName.AUTHORITY.value(), value);
            return this;
        }

        @Override
        public Http2Headers path(ICharSequence value) {
            set(PseudoHeaderName.PATH.value(), value);
            return this;
        }

        @Override
        public Http2Headers status(ICharSequence value) {
            set(PseudoHeaderName.STATUS.value(), value);
            return this;
        }

        @Override
        public ICharSequence method() {
            return get(PseudoHeaderName.METHOD.value());
        }

        @Override
        public ICharSequence scheme() {
            return get(PseudoHeaderName.SCHEME.value());
        }

        @Override
        public ICharSequence authority() {
            return get(PseudoHeaderName.AUTHORITY.value());
        }

        @Override
        public ICharSequence path() {
            return get(PseudoHeaderName.PATH.value());
        }

        @Override
        public ICharSequence status() {
            return get(PseudoHeaderName.STATUS.value());
        }

        @Override
        public bool contains(ICharSequence name, ICharSequence value, bool caseInsensitive) {
            return this.contains(name, value, caseInsensitive? CASE_INSENSITIVE_HASHER : CASE_SENSITIVE_HASHER);
        }

        @Override
        protected final HeaderEntry<ICharSequence, ICharSequence> newHeaderEntry(int h, ICharSequence name, ICharSequence value,
            HeaderEntry<ICharSequence, ICharSequence> next) {
            return new Http2HeaderEntry(h, name, value, next);
        }

        private final class Http2HeaderEntry extends HeaderEntry<ICharSequence, ICharSequence> {
        protected Http2HeaderEntry(int hash, ICharSequence key, ICharSequence value,
            HeaderEntry<ICharSequence, ICharSequence> next) {
            super(hash, key);
            this.value = value;
            this.next = next;

            // Make sure the pseudo headers fields are first in iteration order
            if (key.length() != 0 && key.charAt(0) == ':') {
                after = this.firstNonPseudo;
                before = this.firstNonPseudo.before();
            } else {
                after = head;
                before = head.before();
                if (this.firstNonPseudo == head) {
                    this.firstNonPseudo = this;
                }
            }
            pointNeighborsToThis();
        }

        @Override
        protected void remove() {
            if (this == this.firstNonPseudo) {
                this.firstNonPseudo = this.firstNonPseudo.after();
            }
            super.remove();
        }
    }
}
}
