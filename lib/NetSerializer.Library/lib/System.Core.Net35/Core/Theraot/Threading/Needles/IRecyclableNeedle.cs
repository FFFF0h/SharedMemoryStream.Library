﻿// Needed for NET40

namespace Theraot.Threading.Needles
{
    public interface IRecyclableNeedle<T> : INeedle<T>
    {
        void Free();
    }
}