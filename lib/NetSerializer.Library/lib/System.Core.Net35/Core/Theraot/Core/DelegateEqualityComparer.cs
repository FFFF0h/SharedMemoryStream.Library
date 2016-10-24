﻿#if FAT

using System;
using System.Collections.Generic;

namespace Theraot.Core
{
    public sealed class DelegateEqualityComparer : IEqualityComparer<Delegate>
    {
        private static readonly DelegateEqualityComparer _default = new DelegateEqualityComparer();

        private DelegateEqualityComparer()
        {
            // Empty
        }

        public static DelegateEqualityComparer Default
        {
            get
            {
                return _default;
            }
        }

        public bool Equals(Delegate x, Delegate y)
        {
            return CompareInternal(x, y);
        }

        public int GetHashCode(Delegate obj)
        {
            // obj can be null
            if (ReferenceEquals(obj, null))
            {
                return 0;
            }
            else
            {
                if (ReferenceEquals(obj.Target, null))
                {
                    return obj.Method.GetHashCode();
                }
                else
                {
                    return obj.Method.GetHashCode() ^ obj.Target.GetHashCode();
                }
            }
        }

        private static bool CompareInternal(Delegate x, Delegate y)
        {
            if (ReferenceEquals(x, null))
            {
                return ReferenceEquals(y, null);
            }
            else
            {
                if (ReferenceEquals(y, null))
                {
                    return false;
                }
                else
                {
                    if (!ReferenceEquals(x.Target, y.Target))
                    {
                        return false;
                    }
                    else
                    {
                        return x.Method == y.Method;
                    }
                }
            }
        }
    }
}

#endif