// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE file in the project root for full license information.

namespace DotNetty.Codecs.Http2
{
    using System;
    using System.Collections;
    using System.Collections.Generic;

    sealed class EmptyDictionary<TKey, TValue> : IDictionary<TKey, TValue>
    {
        static readonly NotImplementedException Error = new NotImplementedException();

        public static readonly EmptyDictionary<TKey, TValue> Instance = new EmptyDictionary<TKey, TValue>();

        public EmptyDictionary()
        {
        }

        public IEnumerator<KeyValuePair<TKey, TValue>> GetEnumerator()
        {
            yield break;
        }

        IEnumerator IEnumerable.GetEnumerator()
        {
            return this.GetEnumerator();
        }

        public void Add(KeyValuePair<TKey, TValue> item)
        {
        }

        public void Clear()
        {
        }

        public bool Contains(KeyValuePair<TKey, TValue> item) => false;

        public void CopyTo(KeyValuePair<TKey, TValue>[] array, int arrayIndex)
        {
        }

        public bool Remove(KeyValuePair<TKey, TValue> item) => false;

        public int Count => 0;

        public bool IsReadOnly => false;

        public void Add(TKey key, TValue value)
        {
        }

        public bool ContainsKey(TKey key) => false;

        public bool Remove(TKey key) => false;

        public bool TryGetValue(TKey key, out TValue value)
        {
            value = default(TValue);
            return false;
        }

        public TValue this[TKey key]
        {
            get => throw Error;
            set => throw Error;
        }

        public ICollection<TKey> Keys => throw Error;

        public ICollection<TValue> Values => throw Error;
    }
}