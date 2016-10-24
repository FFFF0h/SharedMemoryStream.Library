﻿#if FAT

using System;
using System.Collections.Generic;
using System.Threading;
using Theraot.Collections.ThreadSafe;
using Theraot.Core;
using Theraot.Threading;

namespace Theraot.Collections.Specialized
{
    [Serializable]
    public sealed partial class FlagArray
    {
        private readonly IReadOnlyCollection<bool> _asReadOnly;
        private readonly int _length;
        private int[] _entries;

        public FlagArray(FlagArray prototype)
        {
            if (ReferenceEquals(prototype, null))
            {
                throw new ArgumentNullException("prototype", "prototype is null.");
            }
            _length = prototype._length;
            _entries = ArrayReservoir<int>.GetArray(GetLength(_length));
            prototype._entries.CopyTo(_entries, 0);
            _asReadOnly = new ExtendedReadOnlyCollection<bool>(this);
        }

        public FlagArray(int length)
        {
            if (length < 0)
            {
                throw new ArgumentOutOfRangeException("length", "length < 0");
            }
            _length = length;
            _entries = ArrayReservoir<int>.GetArray(GetLength(_length));
            _asReadOnly = new ExtendedReadOnlyCollection<bool>(this);
        }

        public FlagArray(int length, bool defaultValue)
            : this(length)
        {
            if (defaultValue)
            {
                Fill(true);
            }
        }

        ~FlagArray()
        {
            if (!GCMonitor.FinalizingForUnload)
            {
                RecycleExtracted();
            }
        }

        public int Count
        {
            get
            {
                return _length;
            }
        }

        public IEnumerable<int> Flags
        {
            get
            {
                var count = 0;
                foreach (var entry in _entries)
                {
                    if (entry == 0)
                    {
                        count += 32;
                        if (count >= _length)
                        {
                            yield break;
                        }
                    }
                    else
                    {
                        foreach (var bit in entry.BinaryReverse().BitsBinary())
                        {
                            if (bit == 1)
                            {
                                yield return count;
                            }
                            count++;
                            if (count == _length)
                            {
                                yield break;
                            }
                        }
                    }
                }
            }
        }

        bool ICollection<bool>.IsReadOnly
        {
            get
            {
                return false;
            }
        }

        public bool this[int index]
        {
            get
            {
                var entryIndex = index >> 5;
                var bit = index & 31;
                var mask = 1 << bit;
                return GetBit(entryIndex, mask);
            }
            set
            {
                var entryIndex = index >> 5;
                var bit = index & 31;
                var mask = 1 << bit;
                if (value)
                {
                    SetBit(entryIndex, mask);
                }
                else
                {
                    UnsetBit(entryIndex, mask);
                }
            }
        }

        public FlagArray Clone()
        {
            return new FlagArray(this);
        }

        public bool Contains(bool item)
        {
            var count = 0;
            var newcount = 0;
            var check = item ? 0 : -1;
            foreach (var entry in _entries)
            {
                newcount += 32;
                if (newcount <= _length)
                {
                    if (entry != check)
                    {
                        return true;
                    }
                    count = newcount;
                }
                else
                {
                    foreach (var bit in entry.BitsBinary())
                    {
                        if ((bit == 1) == item)
                        {
                            count++;
                        }
                        if (count == _length)
                        {
                            break;
                        }
                    }
                    break;
                }
            }
            return false;
        }

        public bool Contains(bool item, IEqualityComparer<bool> comparer)
        {
            return System.Linq.Enumerable.Contains(this, item, comparer);
        }

        public void CopyTo(bool[] array, int arrayIndex)
        {
            Extensions.CanCopyTo(_length, array, arrayIndex);
            Extensions.CopyTo(this, array, arrayIndex);
        }

        public void CopyTo(bool[] array)
        {
            Extensions.CanCopyTo(_length, array);
            Extensions.CopyTo(this, array);
        }

        public void CopyTo(bool[] array, int arrayIndex, int countLimit)
        {
            Extensions.CanCopyTo(array, arrayIndex, countLimit);
            Extensions.CopyTo(this, array, arrayIndex, countLimit);
        }

        public IEnumerator<bool> GetEnumerator()
        {
            var count = 0;
            foreach (var entry in _entries)
            {
                foreach (var bit in entry.BitsBinary())
                {
                    yield return bit == 1;
                    count++;
                    if (count == _length)
                    {
                        yield break;
                    }
                }
            }
        }

        object ICloneable.Clone()
        {
            return Clone();
        }

        void ICollection<bool>.Add(bool item)
        {
            throw new NotSupportedException();
        }

        void ICollection<bool>.Clear()
        {
            throw new NotSupportedException();
        }

        bool ICollection<bool>.Remove(bool item)
        {
            throw new NotSupportedException();
        }

        void IList<bool>.Insert(int index, bool item)
        {
            throw new NotSupportedException();
        }

        void IList<bool>.RemoveAt(int index)
        {
            throw new NotSupportedException();
        }

        public int IndexOf(bool item)
        {
            return Extensions.IndexOf(this, item);
        }

        System.Collections.IEnumerator System.Collections.IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        private void Fill(bool value)
        {
            var entryValue = value ? unchecked((int)0xffffffff) : 0;
            for (var index = 0; index < _length; index++)
            {
                _entries[index] = entryValue;
            }
        }

        private bool GetBit(int index, int mask)
        {
            return (Thread.VolatileRead(ref _entries[index]) & mask) != 0;
        }

        private int GetLength(int length)
        {
            return (length >> 5) + (length & 31) == 0 ? 0 : 1;
        }

        private void RecycleExtracted()
        {
            ArrayReservoir<int>.DonateArray(_entries);
            _entries = null;
        }

        private void SetBit(int index, int mask)
        {
            again:
            var readed = Thread.VolatileRead(ref _entries[index]);
            if ((readed & mask) == 0)
            {
                if (Interlocked.CompareExchange(ref _entries[index], readed | mask, readed) != readed)
                {
                    goto again;
                }
            }
        }

        private void UnsetBit(int index, int mask)
        {
            again:
            var readed = Thread.VolatileRead(ref _entries[index]);
            if ((readed & mask) != 0)
            {
                if (Interlocked.CompareExchange(ref _entries[index], readed & ~mask, readed) != readed)
                {
                    goto again;
                }
            }
        }
    }

    public sealed partial class FlagArray : IList<bool>
#if FAT
        , IExtendedCollection<bool>, ICloneable<FlagArray>
#endif
    {
#if FAT

        IReadOnlyCollection<bool> IExtendedCollection<bool>.AsReadOnly
        {
            get
            {
                return _asReadOnly;
            }
        }

        bool IExtendedCollection<bool>.Remove(bool item, IEqualityComparer<bool> comparer)
        {
            throw new NotSupportedException();
        }

#endif
    }
}

#endif