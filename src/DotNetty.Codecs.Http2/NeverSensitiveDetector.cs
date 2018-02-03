// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using DotNetty.Common.Utilities;

    /**
       * Always return {@code false} for {@link SensitivityDetector#isSensitive(CharSequence, CharSequence)}.
       */
    public class NeverSensitiveDetector : SensitivityDetector
    {
        public bool isSensitive(ICharSequence name, ICharSequence value)
        {
            return false;
        }
    }
}