﻿#if NET35 || NET40

namespace System.Collections.Generic
{
    [global::System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1711:IdentifiersShouldNotHaveIncorrectSuffix", Justification = "By Design")]
    [global::System.Diagnostics.CodeAnalysis.SuppressMessage("Microsoft.Naming", "CA1710:IdentifiersShouldHaveCorrectSuffix", Justification = "By Design")]
    public partial interface IReadOnlyDictionary<TKey, TValue> : IReadOnlyCollection<KeyValuePair<TKey, TValue>>
    {
        IEnumerable<TKey> Keys
        {
            get;
        }

        IEnumerable<TValue> Values
        {
            get;
        }
    }

    public partial interface IReadOnlyDictionary<TKey, TValue>
    {
        TValue this[TKey key]
        {
            get;
        }
    }

    public partial interface IReadOnlyDictionary<TKey, TValue>
    {
        bool ContainsKey(TKey key);

        bool TryGetValue(TKey key, out TValue value);
    }
}

#endif