namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Utilities;

    /**
         * Always return {@code true} for {@link SensitivityDetector#isSensitive(CharSequence, CharSequence)}.
         */
    public class AlwaysSensitiveDetector : SensitivityDetector
    {
        public bool isSensitive(ICharSequence name, ICharSequence value)
        {
            return true;
        }
    }
}