﻿using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MonoTests;
using Theraot.Collections;
using Theraot.Threading.Needles;

namespace Tests.Theraot.Threading.Needles
{
    [TestFixture]
    public class WorkTest
    {
        [Test]
        public void CountdownEvent_Signal_Concurrent()
        {
            for (int r = 0; r < 100; ++r)
            {
                using (var ce = new CountdownEvent(500))
                {
                    for (int i = 0; i < ce.InitialCount; ++i)
                    {
                        Task.Factory.StartNew(delegate
                        {
                            ce.Signal();
                        });
                    }
                    Assert.IsTrue(ce.Wait(1000), "#1");
                }
            }
        }

        [Test]
        [Category("RaceToDeadLock")] // This test creates a race condition, that when resolved sequentially will be stuck
        public void ManualResetEventSlim_SetAfterDisposeTest()
        {
            ManualResetEventSlim mre = new ManualResetEventSlim();

            ParallelTestHelper.Repeat(delegate
            {
                Exception disp = null, setting = null;

                CountdownEvent evt = new CountdownEvent(2);
                CountdownEvent evtFinish = new CountdownEvent(2);

                Task.Factory.StartNew(delegate
                {
                    try
                    {
                        evt.Signal();
                        evt.Wait(1000);
                        mre.Dispose();
                    }
                    catch (Exception e)
                    {
                        disp = e;
                    }
                    evtFinish.Signal();
                });
                Task.Factory.StartNew(delegate
                {
                    try
                    {
                        evt.Signal();
                        evt.Wait(1000);
                        mre.Set();
                    }
                    catch (Exception e)
                    {
                        setting = e;
                    }
                    evtFinish.Signal();
                });

                bool bb = evtFinish.Wait(1000);
                if (!bb)
                    Assert.AreEqual(true, evtFinish.IsSet);

                Assert.IsTrue(bb, "#0");
                Assert.IsNull(disp, "#1");
                Assert.IsNull(setting, "#2");

                evt.Dispose();
                evtFinish.Dispose();
            });
        }

        [Test]
        public void ManualResetEventSlim_Wait_SetConcurrent()
        {
            for (int i = 0; i < 10000; ++i)
            {
                var mre = new ManualResetEventSlim();
                bool b = true;

                Task.Factory.StartNew(delegate
                {
                    mre.Set();
                });

                Task.Factory.StartNew(delegate
                {
                    b &= mre.Wait(1000);
                });

                Assert.IsTrue(mre.Wait(1000), i.ToString());
                Assert.IsTrue(b, i.ToString());
            }
        }

        [Test]
        [Category("RaceToDeadLock")] // This test creates a race condition, that when resolved sequentially will be stuck
        public void Progressor_ThreadedUse()
        {
            var source = new Progressor<int>(new List<int>
            {
                0,
                1,
                2,
                3,
                4,
                5,
                6,
                7,
                8,
                9
            }).AsEnumerable();
            var handle = new ManualResetEvent(false);
            int[] count = { 0, 0, 0 };
            Action work = () =>
            {
                Interlocked.Increment(ref count[0]);
                handle.WaitOne();
                foreach (var item in source)
                {
                    GC.KeepAlive(item);
                    Interlocked.Increment(ref count[2]);
                }
                Interlocked.Increment(ref count[1]);
            };
            Task.Factory.StartNew(work);
            Task.Factory.StartNew(work);
            while (Thread.VolatileRead(ref count[0]) != 2)
            {
                Thread.Sleep(0);
            }
            handle.Set();
            while (Thread.VolatileRead(ref count[1]) != 2)
            {
                Thread.Sleep(0);
            }
            Assert.AreEqual(10, Thread.VolatileRead(ref count[2]));
            handle.Close();
        }

#if FAT
        [Test]
        [Category("RaceToDeadLock")] // This test creates a race condition, that when resolved sequentially will be stuck
        public void Transact_RaceCondition()
        {
            var handle = new ManualResetEvent(false);
            int[] count = { 0, 0 };
            var needle = Transact.CreateNeedle(5);
            var winner = 0;
            Assert.AreEqual(needle.Value, 5);
            Task.Factory.StartNew
            (
                () =>
                {
                    using (var transact = new Transact())
                    {
                        Interlocked.Increment(ref count[0]);
                        handle.WaitOne();
                        needle.Value += 2;
                        if (transact.Commit())
                        {
                            winner = 1;
                        }
                        Interlocked.Increment(ref count[1]);
                    }
                }
            );
            Task.Factory.StartNew
            (
                () =>
                {
                    using (var transact = new Transact())
                    {
                        Interlocked.Increment(ref count[0]);
                        handle.WaitOne();
                        needle.Value += 5;
                        if (transact.Commit())
                        {
                            winner = 2;
                        }
                        Interlocked.Increment(ref count[1]);
                    }
                }
            );
            while (Thread.VolatileRead(ref count[0]) != 2)
            {
                Thread.Sleep(0);
            }
            handle.Set();
            while (Thread.VolatileRead(ref count[1]) != 2)
            {
                Thread.Sleep(0);
            }
            // One, the other, or both
            Trace.WriteLine("Winner: " + winner);
            Trace.WriteLine("Value: " + needle.Value);
            Assert.IsTrue((winner == 1 && needle.Value == 7) || (winner == 2 && needle.Value == 10) || (needle.Value == 12));
            handle.Close();
        }
#endif
    }
}