#if NET35

using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using Theraot.Core;

namespace System
{
    [DebuggerDisplay("ThreadSafetyMode={Mode}, IsValueCreated={IsValueCreated}, IsValueFaulted={IsValueFaulted}, Value={ValueForDebugDisplay}")]
    [Serializable]
    public class Lazy<T>
    {
        private int _isValueCreated;
        private T _target;
        private Func<T> _valueFactory;

        public Lazy()
            : this(LazyThreadSafetyMode.ExecutionAndPublication)
        {
            //Empty
        }

        public Lazy(Func<T> valueFactory)
            : this(valueFactory, LazyThreadSafetyMode.ExecutionAndPublication)
        {
            //Empty
        }

        public Lazy(bool isThreadSafe)
            : this(isThreadSafe ? LazyThreadSafetyMode.ExecutionAndPublication : LazyThreadSafetyMode.None)
        {
            //Empty
        }

        public Lazy(LazyThreadSafetyMode mode)
            : this(TypeHelper.GetCreateOrFail<T>(), mode, false)
        {
            //Empty
        }

        public Lazy(Func<T> valueFactory, bool isThreadSafe)
            : this(valueFactory, isThreadSafe ? LazyThreadSafetyMode.ExecutionAndPublication : LazyThreadSafetyMode.None)
        {
            //Empty
        }

        public Lazy(Func<T> valueFactory, LazyThreadSafetyMode mode)
            : this(valueFactory, mode, true)
        {
            //Empty
        }

        private Lazy(Func<T> valueFactory, LazyThreadSafetyMode mode, bool cacheExceptions)
        {
            var __valueFactory = Check.NotNullArgument(valueFactory, "valueFactory");
            switch (mode)
            {
                case LazyThreadSafetyMode.None:
                    {
                        if (cacheExceptions)
                        {
                            var threads = new HashSet<Thread>();
                            _valueFactory =
                                () => CachingNoneMode(__valueFactory, threads);
                        }
                        else
                        {
                            var threads = new HashSet<Thread>();
                            _valueFactory =
                                () => NoneMode(__valueFactory, threads);
                        }
                    }
                    break;

                case LazyThreadSafetyMode.PublicationOnly:
                    {
                        _valueFactory =
                            () => PublicationOnlyMode(__valueFactory);
                    }
                    break;

                default: /*LazyThreadSafetyMode.ExecutionAndPublication*/
                    {
                        if (cacheExceptions)
                        {
                            Thread thread = null;
                            var waitHandle = new ManualResetEvent(false);
                            _valueFactory =
                                () => CachingFullMode(__valueFactory, waitHandle, ref thread);
                        }
                        else
                        {
                            Thread thread = null;
                            var waitHandle = new ManualResetEvent(false);
                            int preIsValueCreated = 0;
                            _valueFactory =
                                () => FullMode(__valueFactory, waitHandle, ref thread, ref preIsValueCreated);
                        }
                    }
                    break;
            }
        }

        public bool IsValueCreated
        {
            get
            {
                return _isValueCreated == 1;
            }
        }

        [DebuggerBrowsable(DebuggerBrowsableState.Never)]
        public T Value
        {
            get
            {
                return _valueFactory.Invoke();
            }
        }

        internal T ValueForDebugDisplay
        {
            get
            {
                return _target;
            }
        }

        private T CachingFullMode(Func<T> valueFactory, ManualResetEvent waitHandle, ref Thread thread)
        {
            if (Interlocked.CompareExchange(ref _isValueCreated, 1, 0) == 0)
            {
                try
                {
                    thread = Thread.CurrentThread;
                    GC.KeepAlive(thread);
                    _target = valueFactory.Invoke();
                    _valueFactory = FuncHelper.GetReturnFunc(_target);
                    return _target;
                }
                catch (Exception exc)
                {
                    _valueFactory = FuncHelper.GetThrowFunc<T>(exc);
                    throw;
                }
                finally
                {
                    waitHandle.Set();
                    thread = null;
                }
            }
            else
            {
                if (ReferenceEquals(thread, Thread.CurrentThread))
                {
                    throw new InvalidOperationException();
                }
                waitHandle.WaitOne();
                return _valueFactory.Invoke();
            }
        }

        private T CachingNoneMode(Func<T> valueFactory, HashSet<Thread> threads)
        {
            Thread currentThread = Thread.CurrentThread;
            if (Thread.VolatileRead(ref _isValueCreated) == 0)
            {
                try
                {
                    // lock (threads) // This is meant to not be thread-safe
                    {
                        if (threads.Contains(currentThread))
                        {
                            throw new InvalidOperationException();
                        }
                        threads.Add(currentThread);
                    }
                    _target = valueFactory();
                    _valueFactory = FuncHelper.GetReturnFunc(_target);
                    Thread.VolatileWrite(ref _isValueCreated, 1);
                    return _target;
                }
                catch (Exception exception)
                {
                    _valueFactory = FuncHelper.GetThrowFunc<T>(exception);
                    throw;
                }
                finally
                {
                    // lock (threads) // This is meant to not be thread-safe
                    {
                        threads.Remove(Thread.CurrentThread);
                    }
                }
            }
            else
            {
                return _valueFactory.Invoke();
            }
        }

        private T FullMode(Func<T> valueFactory, ManualResetEvent waitHandle, ref Thread thread, ref int preIsValueCreated)
        {
            back:
            if (Interlocked.CompareExchange(ref preIsValueCreated, 1, 0) == 0)
            {
                try
                {
                    thread = Thread.CurrentThread;
                    GC.KeepAlive(thread);
                    _target = valueFactory.Invoke();
                    _valueFactory = FuncHelper.GetReturnFunc(_target);
                    Thread.VolatileWrite(ref _isValueCreated, 1);
                    return _target;
                }
                catch (Exception)
                {
                    Thread.VolatileWrite(ref preIsValueCreated, 0);
                    throw;
                }
                finally
                {
                    waitHandle.Set();
                    thread = null;
                }
            }
            else
            {
                if (ReferenceEquals(thread, Thread.CurrentThread))
                {
                    throw new InvalidOperationException();
                }
                else
                {
                    waitHandle.WaitOne();
                    if (Thread.VolatileRead(ref _isValueCreated) == 1)
                    {
                        return _valueFactory.Invoke();
                    }
                    else
                    {
                        goto back;
                    }
                }
            }
        }

        private T NoneMode(Func<T> valueFactory, HashSet<Thread> threads)
        {
            Thread currentThread = Thread.CurrentThread;
            if (Thread.VolatileRead(ref _isValueCreated) == 0)
            {
                try
                {
                    // lock (threads) // This is meant to not be thread-safe
                    {
                        if (threads.Contains(currentThread))
                        {
                            throw new InvalidOperationException();
                        }
                        else
                        {
                            threads.Add(currentThread);
                        }
                    }
                    _target = valueFactory();
                    _valueFactory = FuncHelper.GetReturnFunc(_target);
                    Thread.VolatileWrite(ref _isValueCreated, 1);
                    return _target;
                }
                catch (Exception)
                {
                    Thread.VolatileWrite(ref _isValueCreated, 0);
                    throw;
                }
                finally
                {
                    // lock (threads) // This is meant to not be thread-safe
                    {
                        threads.Remove(Thread.CurrentThread);
                    }
                }
            }
            else
            {
                return _valueFactory.Invoke();
            }
        }

        private T PublicationOnlyMode(Func<T> valueFactory)
        {
            _target = valueFactory();
            if (Interlocked.CompareExchange(ref _isValueCreated, 1, 0) == 0)
            {
                _valueFactory = FuncHelper.GetReturnFunc(_target);
            }
            return _target;
        }
    }
}

#endif