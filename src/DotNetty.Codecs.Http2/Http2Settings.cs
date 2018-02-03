// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections.Generic;

    /**
 * Settings for one endpoint in an HTTP/2 connection. Each of the values are optional as defined in
 * the spec for the SETTINGS frame. Permits storage of arbitrary key/value pairs but provides helper
 * methods for standard settings.
 */
public class Http2Settings : Dictionary<char, long>// : CharObjectHashMap<long> 
{
    /**
     * Default capacity based on the number of standard settings from the HTTP/2 spec, adjusted so that adding all of
     * the standard settings will not cause the map capacity to change.
     */
    static readonly long FALSE = 0L;
    static readonly long True = 1L;

    public Http2Settings() 
        : this(Http2CodecUtil.NUM_STANDARD_SETTINGS)
    {
    }


    public Http2Settings(int initialCapacity)
        : base(initialCapacity)
    {}

    /**
     * Adds the given setting key/value pair. For standard settings defined by the HTTP/2 spec, performs
     * validation on the values.
     *
     * @throws ArgumentException if verification for a standard HTTP/2 setting fails.
     */
    public long put(char key, long value) 
    {
        verifyStandardSetting(key, value);
        return base[key] = value;
    }

    /**
     * Gets the {@code Http2CodecUtil.SETTINGS_HEADER_TABLE_SIZE} value. If unavailable, returns {@code null}.
     */
    public long headerTableSize()
    {
        return this.TryGetValue(Http2CodecUtil.SETTINGS_HEADER_TABLE_SIZE, out var val) ? val : default(long);
    }

    /**
     * Sets the {@code Http2CodecUtil.SETTINGS_HEADER_TABLE_SIZE} value.
     *
     * @throws ArgumentException if verification of the setting fails.
     */
    public Http2Settings headerTableSize(long value) {
        this[Http2CodecUtil.SETTINGS_HEADER_TABLE_SIZE] = value;
        return this;
    }

    /**
     * Gets the {@code Http2CodecUtil.SETTINGS_ENABLE_PUSH} value. If unavailable, returns {@code null}.
     */
    public bool pushEnabled()
    {
        return this.TryGetValue(Http2CodecUtil.SETTINGS_ENABLE_PUSH, out var val) ? val == True : false;
    }

    /**
     * Sets the {@code Http2CodecUtil.SETTINGS_ENABLE_PUSH} value.
     */
    public Http2Settings pushEnabled(bool enabled) {
        this[Http2CodecUtil.SETTINGS_ENABLE_PUSH] = enabled ? True : FALSE ;
        return this;
    }

    /**
     * Gets the {@code Http2CodecUtil.SETTINGS_MAX_CONCURRENT_STREAMS} value. If unavailable, returns {@code null}.
     */
    public long maxConcurrentStreams()
    {
        return this.TryGetValue(Http2CodecUtil.SETTINGS_MAX_CONCURRENT_STREAMS, out var val) ? val : default(long);
    }

    /**
     * Sets the {@code Http2CodecUtil.SETTINGS_MAX_CONCURRENT_STREAMS} value.
     *
     * @throws ArgumentException if verification of the setting fails.
     */
    public Http2Settings maxConcurrentStreams(long value) {
        this[Http2CodecUtil.SETTINGS_MAX_CONCURRENT_STREAMS] = value;
        return this;
    }

    /**
     * Gets the {@code Http2CodecUtil.SETTINGS_INITIAL_WINDOW_SIZE} value. If unavailable, returns {@code null}.
     */
    public int initialWindowSize() {
        return getIntValue(Http2CodecUtil.SETTINGS_INITIAL_WINDOW_SIZE);
    }

    /**
     * Sets the {@code Http2CodecUtil.SETTINGS_INITIAL_WINDOW_SIZE} value.
     *
     * @throws ArgumentException if verification of the setting fails.
     */
    public Http2Settings initialWindowSize(int value) {
        this[Http2CodecUtil.SETTINGS_INITIAL_WINDOW_SIZE] = value;
        return this;
    }

    /**
     * Gets the {@code Http2CodecUtil.SETTINGS_MAX_FRAME_SIZE} value. If unavailable, returns {@code null}.
     */
    public int maxFrameSize() {
        return getIntValue(Http2CodecUtil.SETTINGS_MAX_FRAME_SIZE);
    }

    /**
     * Sets the {@code Http2CodecUtil.SETTINGS_MAX_FRAME_SIZE} value.
     *
     * @throws ArgumentException if verification of the setting fails.
     */
    public Http2Settings maxFrameSize(int value) {
        this[Http2CodecUtil.SETTINGS_MAX_FRAME_SIZE] = value;
        return this;
    }

    /**
     * Gets the {@code Http2CodecUtil.SETTINGS_MAX_HEADER_LIST_SIZE} value. If unavailable, returns {@code null}.
     */
    public long maxHeaderListSize()
    {
        return this.TryGetValue(Http2CodecUtil.SETTINGS_MAX_HEADER_LIST_SIZE, out var val) ? val : default(long);
    }

    /**
     * Sets the {@code Http2CodecUtil.SETTINGS_MAX_HEADER_LIST_SIZE} value.
     *
     * @throws ArgumentException if verification of the setting fails.
     */
    public Http2Settings maxHeaderListSize(long value) {
        this[Http2CodecUtil.SETTINGS_MAX_HEADER_LIST_SIZE] = value;
        return this;
    }

    /**
     * Clears and then copies the given settings into this object.
     */
    public Http2Settings copyFrom(Http2Settings settings) {
        this.Clear();
        foreach (KeyValuePair<char,long> pair in settings)
        {
            this[pair.Key] = pair.Value;
        }
        return this;
    }

    /**
     * A helper method that returns {@link long#intValue()} on the return of {@link #get(char)}, if present. Note that
     * if the range of the value exceeds {@link int#Http2CodecUtil.MAX_VALUE}, the {@link #get(char)} method should
     * be used instead to avoid truncation of the value.
     */
    public int getIntValue(char key)
    {
        return this.TryGetValue(key, out var val) ? (int)val : default(int);
    }

    static void verifyStandardSetting(int key, long value) {
        switch (key) {
            case Http2CodecUtil.SETTINGS_HEADER_TABLE_SIZE:
                if (value < Http2CodecUtil.MIN_HEADER_TABLE_SIZE || value > Http2CodecUtil.MAX_HEADER_TABLE_SIZE) {
                    throw new ArgumentException("Setting HEADER_TABLE_SIZE is invalid: " + value);
                }
                break;
            case Http2CodecUtil.SETTINGS_ENABLE_PUSH:
                if (value != 0L && value != 1L) {
                    throw new ArgumentException("Setting ENABLE_PUSH is invalid: " + value);
                }
                break;
            case Http2CodecUtil.SETTINGS_MAX_CONCURRENT_STREAMS:
                if (value < Http2CodecUtil.MIN_CONCURRENT_STREAMS || value > Http2CodecUtil.MAX_CONCURRENT_STREAMS) {
                    throw new ArgumentException(
                            "Setting Http2CodecUtil.MAX_CONCURRENT_STREAMS is invalid: " + value);
                }
                break;
            case Http2CodecUtil.SETTINGS_INITIAL_WINDOW_SIZE:
                if (value < Http2CodecUtil.MIN_INITIAL_WINDOW_SIZE || value > Http2CodecUtil.MAX_INITIAL_WINDOW_SIZE) {
                    throw new ArgumentException("Setting INITIAL_WINDOW_SIZE is invalid: "
                            + value);
                }
                break;
            case Http2CodecUtil.SETTINGS_MAX_FRAME_SIZE:
                if (!Http2CodecUtil.isMaxFrameSizeValid(value)) {
                    throw new ArgumentException("Setting Http2CodecUtil.MAX_FRAME_SIZE is invalid: " + value);
                }
                break;
            case Http2CodecUtil.SETTINGS_MAX_HEADER_LIST_SIZE:
                if (value < Http2CodecUtil.MIN_HEADER_LIST_SIZE || value > Http2CodecUtil.MAX_HEADER_LIST_SIZE) {
                    throw new ArgumentException("Setting Http2CodecUtil.MAX_HEADER_LIST_SIZE is invalid: " + value);
                }
                break;
            default:
                // Non-standard HTTP/2 setting - don't do validation.
                break;
        }
    }

    
    protected string keyToString(char key) {
        switch (key) {
            case Http2CodecUtil.SETTINGS_HEADER_TABLE_SIZE:
                return "HEADER_TABLE_SIZE";
            case Http2CodecUtil.SETTINGS_ENABLE_PUSH:
                return "ENABLE_PUSH";
            case Http2CodecUtil.SETTINGS_MAX_CONCURRENT_STREAMS:
                return "Http2CodecUtil.MAX_CONCURRENT_STREAMS";
            case Http2CodecUtil.SETTINGS_INITIAL_WINDOW_SIZE:
                return "INITIAL_WINDOW_SIZE";
            case Http2CodecUtil.SETTINGS_MAX_FRAME_SIZE:
                return "Http2CodecUtil.MAX_FRAME_SIZE";
            case Http2CodecUtil.SETTINGS_MAX_HEADER_LIST_SIZE:
                return "Http2CodecUtil.MAX_HEADER_LIST_SIZE";
            default:
                // Unknown keys.
                return base.ToString();
        }
    }

    public static Http2Settings defaultSettings() {
        return new Http2Settings().maxHeaderListSize(Http2CodecUtil.DEFAULT_HEADER_LIST_SIZE);
    }
}
}