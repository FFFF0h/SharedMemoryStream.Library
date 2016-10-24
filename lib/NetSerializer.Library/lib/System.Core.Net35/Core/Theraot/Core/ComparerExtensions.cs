﻿// Needed for Workaround

using System;
using System.Collections.Generic;

using Theraot.Collections.Specialized;

namespace Theraot.Core
{
    public static class ComparerExtensions
    {
        public static IComparer<T> ToComparer<T>(this Comparison<T> comparison)
        {
            // Replacement for Comparer.Create(Comparison<T>) added in .NET 4.5
            return new CustomComparer<T>(comparison);
        }

#if FAT

        public static IComparer<T> LinkComparer<T>(this IComparer<T> comparer, IComparer<T> linkedComparer)
        {
            return new ComparerLinked<T>(comparer, linkedComparer);
        }

        public static IComparer<T> LinkComparer<T>(this IComparer<T> comparer, Func<T, T, int> linkedComparer)
        {
            return new ComparerLinked<T>(comparer, linkedComparer.ToComparer());
        }

        public static IComparer<T> Reverse<T>(this IComparer<T> comparer)
        {
            var originalAsReverse = comparer as ReverseComparer<T>;
            if (ReferenceEquals(originalAsReverse, null))
            {
                return new ReverseComparer<T>(comparer ?? Comparer<T>.Default);
            }
            else
            {
                return originalAsReverse.Wrapped;
            }
        }

        public static IComparer<T> ToComparer<T>(this Func<T, T, int> comparison)
        {
            return new CustomComparer<T>(comparison);
        }

        private class ComparerLinked<T> : IComparer<T>
        {
            private readonly IComparer<T> _first;
            private readonly IComparer<T> _second;

            public ComparerLinked(IComparer<T> first, IComparer<T> second)
            {
                _first = Check.NotNullArgument(first, "first");
                _second = Check.NotNullArgument(second, "second");
            }

            public int Compare(T x, T y)
            {
                var result = _first.Compare(x, y);
                if (result == 0)
                {
                    result = _second.Compare(x, y);
                }
                return result;
            }
        }

        private class ReverseComparer<T> : IComparer<T>
        {
            private readonly IComparer<T> _wrapped;

            public ReverseComparer(IComparer<T> wrapped)
            {
                _wrapped = Check.NotNullArgument(wrapped, "wrapped");
            }

            internal IComparer<T> Wrapped
            {
                get
                {
                    return _wrapped;
                }
            }

            public int Compare(T x, T y)
            {
                return _wrapped.Compare(y, x);
            }
        }

#endif
    }
}