﻿#if NET35

using System.Collections.Generic;

namespace System.Collections.Concurrent
{
    public interface IProducerConsumerCollection<T> : IEnumerable<T>, ICollection
    {
        void CopyTo(T[] array, int index);

        T[] ToArray();

        bool TryAdd(T item);

        bool TryTake(out T item);
    }
}

#endif