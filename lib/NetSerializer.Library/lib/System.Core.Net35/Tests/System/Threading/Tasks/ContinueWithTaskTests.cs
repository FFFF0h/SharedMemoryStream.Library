﻿using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using NUnit.Framework;

namespace MonoTests.System.Threading.Tasks
{
    [TestFixture]
    public class ContinueTaskTests
    {
        [Test]
        public void ContinueWithInvalidArguments ()
        {
            var task = new Task (() => { });
            try {
                task.ContinueWith (null);
                Assert.Fail ("#1");
            } catch (ArgumentNullException e) {
            }

            try {
                task.ContinueWith (delegate { }, null);
                Assert.Fail ("#2");
            } catch (ArgumentNullException e) {
            }

            try {
                task.ContinueWith (delegate { }, TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.NotOnCanceled);
                Assert.Fail ("#3");
            } catch (ArgumentOutOfRangeException) {
            }

            try {
                task.ContinueWith (delegate { }, TaskContinuationOptions.OnlyOnRanToCompletion | TaskContinuationOptions.NotOnRanToCompletion);
                Assert.Fail ("#4");
            } catch (ArgumentOutOfRangeException) {
            }
        }

        [Test]
        public void ContinueWithOnAnyTestCase ()
        {
            ParallelTestHelper.Repeat (delegate {
                bool result = false;

                Task t = Task.Factory.StartNew (delegate { });
                Task cont = t.ContinueWith (delegate { result = true; }, TaskContinuationOptions.None);
                Assert.IsTrue (t.Wait (2000), "First wait, (status, {0})", t.Status);
                Assert.IsTrue (cont.Wait (2000), "Cont wait, (result, {0}) (parent status, {2}) (status, {1})", result, cont.Status, t.Status);
                Assert.IsNull (cont.Exception, "#1");
                Assert.IsNotNull (cont, "#2");
                Assert.IsTrue (result, "#3");
            });
        }

        [Test]
        public void ContinueWithOnCompletedSuccessfullyTestCase ()
        {
            ParallelTestHelper.Repeat (delegate {
                bool result = false;

                Task t = Task.Factory.StartNew (delegate { });
                Task cont = t.ContinueWith (delegate { result = true; }, TaskContinuationOptions.OnlyOnRanToCompletion);
                Assert.IsTrue (t.Wait (1000), "#4");
                Assert.IsTrue (cont.Wait (1000), "#5");

                Assert.IsNull (cont.Exception, "#1");
                Assert.IsNotNull (cont, "#2");
                Assert.IsTrue (result, "#3");
            });
        }

        [Test]
        public void ContinueWithOnAbortedTestCase ()
        {
            bool result = false;
            bool taskResult = false;

            CancellationTokenSource src = new CancellationTokenSource ();
            Task t = new Task (delegate { taskResult = true; }, src.Token);

            Task cont = t.ContinueWith (delegate { result = true; },
                TaskContinuationOptions.OnlyOnCanceled | TaskContinuationOptions.ExecuteSynchronously);

            src.Cancel ();

            Assert.AreEqual (TaskStatus.Canceled, t.Status, "#1a");
            Assert.IsTrue (cont.IsCompleted, "#1b");
            Assert.IsTrue (result, "#1c");

            try {
                t.Start ();
                Assert.Fail ("#2");
            } catch (InvalidOperationException) {
            }

            Assert.IsTrue (cont.Wait (1000), "#3");

            Assert.IsFalse (taskResult, "#4");

            Assert.IsNull (cont.Exception, "#5");
            Assert.AreEqual (TaskStatus.RanToCompletion, cont.Status, "#6");
        }

        [Test]
        [Category ("NotWorking")] // This task relies on a race condition and the ThreadPool is too slow to schedule tasks prior to .NET 4.0 - this succeds if serialized
        [Category ("ThreadPool")]
        public void ContinueWithOnFailedTestCase ()
        {
            ParallelTestHelper.Repeat (delegate {
                bool result = false;

                Task t = Task.Factory.StartNew (delegate { throw new Exception ("foo"); });
                Task cont = t.ContinueWith (delegate { result = true; }, TaskContinuationOptions.OnlyOnFaulted);

                Assert.IsTrue (cont.Wait (1000), "#0");
                Assert.IsNotNull (t.Exception, "#1");
                Assert.IsNotNull (cont, "#2");
                Assert.IsTrue (result, "#3");
            });
        }

        [Test]
        public void ContinueWithWithStart ()
        {
            Task t = new Task<int> (() => 1);
            t = t.ContinueWith (l => { });
            try {
                t.Start ();
                Assert.Fail ();
            } catch (InvalidOperationException) {
            }
        }

        [Test]
        public void ContinueWithChildren ()
        {
            ParallelTestHelper.Repeat (delegate {
                bool result = false;

                var t = Task.Factory.StartNew (() => Task.Factory.StartNew (() => { }, TaskCreationOptions.AttachedToParent));

                var mre = new ManualResetEvent (false);
                t.ContinueWith (l => {
                    result = true;
                    mre.Set ();
                });

                Assert.IsTrue (mre.WaitOne (1000), "#1");
                Assert.IsTrue (result, "#2");
            }, 2);
        }

        [Test]
        [Category ("NotWorking")] // This task relies on a race condition and the ThreadPool is too slow to schedule tasks prior to .NET 4.0 - this succeds if serialized
        [Category ("ThreadPool")]
        public void ContinueWithDifferentOptionsAreCanceledTest ()
        {
            var mre = new ManualResetEventSlim ();
            var task = Task.Factory.StartNew (() => mre.Wait (200));
            var contFailed = task.ContinueWith (t => { }, TaskContinuationOptions.OnlyOnFaulted);
            var contCanceled = task.ContinueWith (t => { }, TaskContinuationOptions.OnlyOnCanceled);
            var contSuccess = task.ContinueWith (t => { }, TaskContinuationOptions.OnlyOnRanToCompletion);

            mre.Set ();
            contSuccess.Wait (100);

            Assert.IsTrue (contSuccess.IsCompleted);
            Assert.IsTrue (contFailed.IsCompleted);
            Assert.IsTrue (contCanceled.IsCompleted);
            Assert.IsFalse (contSuccess.IsCanceled);
            Assert.IsTrue (contFailed.IsCanceled);
            Assert.IsTrue (contCanceled.IsCanceled);
        }

        Task parent_wfc;

        [Test]
        public void WaitingForChildrenToComplete ()
        {
            Task nested = null;
            var mre = new ManualResetEvent (false);

            parent_wfc = Task.Factory.StartNew (() => {
                nested = Task.Factory.StartNew (() => {
                    Assert.IsTrue (mre.WaitOne (4000), "parent_wfc needs to be set first");
                    Assert.IsFalse (parent_wfc.Wait (10), "#1a");
                    Assert.AreEqual (TaskStatus.WaitingForChildrenToComplete, parent_wfc.Status, "#1b");
                }, TaskCreationOptions.AttachedToParent).ContinueWith (l => {
                    Assert.IsTrue (parent_wfc.Wait (2000), "#2a");
                    Assert.AreEqual (TaskStatus.RanToCompletion, parent_wfc.Status, "#2b");
                }, TaskContinuationOptions.ExecuteSynchronously);
            });

            mre.Set ();
            Assert.IsTrue (parent_wfc.Wait (2000), "#3");
            Assert.IsTrue (nested.Wait (2000), "#4");
        }

        [Test]
        public void WaitChildWithContinuationAttachedTest ()
        {
            bool result = false;
            var task = new Task (() => {
                Task.Factory.StartNew (() => {
                    Thread.Sleep (200);
                }, TaskCreationOptions.AttachedToParent).ContinueWith (t => {
                    Thread.Sleep (200);
                    result = true;
                }, TaskContinuationOptions.AttachedToParent);
            });
            task.Start ();
            task.Wait ();
            Assert.IsTrue (result);
        }

        [Test]
        public void WaitChildWithContinuationNotAttachedTest ()
        {
            var task = new Task (() => {
                Task.Factory.StartNew (() => {
                    Thread.Sleep (200);
                }, TaskCreationOptions.AttachedToParent).ContinueWith (t => {
                    Thread.Sleep (3000);
                });
            });
            task.Start ();
            Assert.IsTrue (task.Wait (400));
        }

        [Test]
        public void RunSynchronouslyOnContinuation ()
        {
            Task t = new Task<int> (() => 1);
            t = t.ContinueWith (l => { });
            try {
                t.RunSynchronously ();
                Assert.Fail ("#1");
            } catch (InvalidOperationException) {
            }
        }

        [Test]
        [Category ("NotWorking")] // This task relies on a race condition and the ThreadPool is too slow to schedule tasks prior to .NET 4.0  - this fails if serialized
        [Category ("ThreadPool")]
        public void CanceledContinuationExecuteSynchronouslyTest ()
        {
            var source = new CancellationTokenSource ();
            var token = source.Token;
            var evt = new ManualResetEventSlim ();
            bool result = false;
            bool thrown = false;

            var task = Task.Factory.StartNew (() => evt.Wait (100));
            var cont = task.ContinueWith (t => result = true, token, TaskContinuationOptions.ExecuteSynchronously, TaskScheduler.Default);

            source.Cancel ();
            evt.Set ();
            task.Wait (100);
            try {
                cont.Wait (100);
            } catch (Exception ex) {
                thrown = true;
            }

            Assert.IsTrue (task.IsCompleted);
            Assert.IsTrue (cont.IsCanceled);
            Assert.IsFalse (result);
            Assert.IsTrue (thrown);
        }

        [Test]
        public void WhenChildTaskErrorIsThrownOnlyOnFaultedContinuationShouldExecute ()
        {
            var continuationRan = false;
            var testTask = new Task (() => {
                var task = new Task (() => {
                    throw new InvalidOperationException ();
                }, TaskCreationOptions.AttachedToParent);
                task.RunSynchronously ();
            });
            var onErrorTask = testTask.ContinueWith (x => continuationRan = true, TaskContinuationOptions.OnlyOnFaulted);
            testTask.RunSynchronously ();
            onErrorTask.Wait (100);
            Assert.IsTrue (continuationRan);
        }

        [Test]
        public void WhenChildTaskErrorIsThrownNotOnFaultedContinuationShouldNotBeExecuted ()
        {
            var continuationRan = false;
            var testTask = new Task (() => {
                var task = new Task (() => {
                    throw new InvalidOperationException ();
                }, TaskCreationOptions.AttachedToParent);
                task.RunSynchronously ();
            });
            var onErrorTask = testTask.ContinueWith (x => continuationRan = true, TaskContinuationOptions.NotOnFaulted);
            testTask.RunSynchronously ();
            Assert.IsTrue (onErrorTask.IsCompleted);
            Assert.IsFalse (onErrorTask.IsFaulted);
            Assert.IsFalse (continuationRan);
        }

        [Test]
        public void WhenChildTaskSeveralLevelsDeepHandlesAggregateExceptionErrorStillBubblesToParent ()
        {
            var continuationRan = false;
            AggregateException e = null;
            var testTask = new Task (() => {
                var child1 = new Task (() => {
                    var child2 = new Task (() => {
                        throw new InvalidOperationException ();
                    }, TaskCreationOptions.AttachedToParent);
                    child2.RunSynchronously ();
                }, TaskCreationOptions.AttachedToParent);

                child1.RunSynchronously ();
                e = child1.Exception;
                child1.Exception.Handle (ex => true);
            });
            var onErrorTask = testTask.ContinueWith (x => continuationRan = true, TaskContinuationOptions.OnlyOnFaulted);
            testTask.RunSynchronously ();
            onErrorTask.Wait (1000);
            Assert.IsNotNull (e);
            Assert.IsTrue (continuationRan);
        }

        [Test]
        public void AlreadyCompletedChildTaskShouldRunContinuationImmediately ()
        {
            string result = "Failed";
            var testTask = new Task (() => {
                var child = new Task<string> (() => {
                    return "Success";
                }, TaskCreationOptions.AttachedToParent);
                child.RunSynchronously ();
                child.ContinueWith (x => { Thread.Sleep (50); result = (x as Task<string>).Result; }, TaskContinuationOptions.AttachedToParent | TaskContinuationOptions.NotOnFaulted);
            });
            testTask.RunSynchronously ();

            Assert.AreEqual ("Success", result);
        }

#if NET35 || NET45
        [Test]
        [Category("NotWorking")] // This task relies on a race condition and the ThreadPool is too slow to schedule tasks prior to .NET 4.0 - this succeds if serialized
        [Category("ThreadPool")]
        public void ContinuationOnBrokenScheduler()
        {
            var s = new ExceptionScheduler();
            Task t = new Task(delegate { });

            var t2 = t.ContinueWith(delegate {
            }, TaskContinuationOptions.ExecuteSynchronously, s);

            var t3 = t.ContinueWith(delegate {
            }, TaskContinuationOptions.ExecuteSynchronously, s);

            t.Start();

            try
            {
                Assert.IsTrue(t3.Wait(2000), "#0");
                Assert.Fail("#1");
            }
            catch (AggregateException e)
            {
            }

            Assert.AreEqual(TaskStatus.Faulted, t2.Status, "#2");
            Assert.AreEqual(TaskStatus.Faulted, t3.Status, "#3");
        }

        [Test]
        public void ContinueWith_StateValue()
        {
            var t = Task.Factory.StartNew(l => {
                Assert.AreEqual(1, l, "a-1");
            }, 1);

            var c = t.ContinueWith((a, b) => {
                Assert.AreEqual(t, a, "c-1");
                Assert.AreEqual(2, b, "c-2");
            }, 2);

            var d = t.ContinueWith((a, b) => {
                Assert.AreEqual(t, a, "d-1");
                Assert.AreEqual(3, b, "d-2");
                return 77;
            }, 3);

            Assert.IsTrue(d.Wait(1000), "#1");

            Assert.AreEqual(1, t.AsyncState, "#2");
            Assert.AreEqual(2, c.AsyncState, "#3");
            Assert.AreEqual(3, d.AsyncState, "#4");
        }

        [Test]
        public void ContinueWith_StateValueGeneric()
        {
            var t = Task<int>.Factory.StartNew(l => {
                Assert.AreEqual(1, l, "a-1");
                return 80;
            }, 1);

            var c = t.ContinueWith((a, b) => {
                Assert.AreEqual(t, a, "c-1");
                Assert.AreEqual(2, b, "c-2");
                return "c";
            }, 2);

            var d = t.ContinueWith((a, b) => {
                Assert.AreEqual(t, a, "d-1");
                Assert.AreEqual(3, b, "d-2");
                return 'd';
            }, 3);

            Assert.IsTrue(d.Wait(1000), "#1");

            Assert.AreEqual(1, t.AsyncState, "#2");
            Assert.AreEqual(80, t.Result, "#2r");
            Assert.AreEqual(2, c.AsyncState, "#3");
            Assert.AreEqual("c", c.Result, "#3r");
            Assert.AreEqual(3, d.AsyncState, "#4");
            Assert.AreEqual('d', d.Result, "#3r");
        }

#endif

        [Test]
        public void ContinueWith_CustomScheduleRejected ()
        {
            var scheduler = new NonInlineableScheduler ();
            var t = Task.Factory.StartNew (delegate { }).
                ContinueWith (r => { }, CancellationToken.None, TaskContinuationOptions.ExecuteSynchronously, scheduler);

            Assert.IsTrue (t.Wait (5000));
        }

#if NET35 || NET45

        [Test]
        public void LazyCancelationTest()
        {
            var source = new CancellationTokenSource();
            source.Cancel();
            var parent = new Task(delegate { });
            var cont = parent.ContinueWith(delegate { }, source.Token, TaskContinuationOptions.LazyCancellation, TaskScheduler.Default);

            Assert.AreNotEqual(TaskStatus.Canceled, cont.Status, "#1");
            parent.Start();
            try
            {
                Assert.IsTrue(cont.Wait(1000), "#2");
                Assert.Fail();
            }
            catch (AggregateException ex)
            {
                Assert.That(ex.InnerException, Is.TypeOf(typeof(TaskCanceledException)), "#3");
            }
        }

        [Test]
        public void ChildTaskWithUnscheduledContinuationAttachedToParent()
        {
            Task inner = null;
            var child = Task.Factory.StartNew(() => {
                inner = Task.Run(() => {
                    throw new ApplicationException();
                }).ContinueWith(task => { }, TaskContinuationOptions.AttachedToParent | TaskContinuationOptions.NotOnFaulted | TaskContinuationOptions.ExecuteSynchronously);
            });

            int counter = 0;
            var t = child.ContinueWith(t2 => ++counter, TaskContinuationOptions.ExecuteSynchronously);
            Assert.IsTrue(t.Wait(5000), "#1");
            Assert.AreEqual(1, counter, "#2");
            Assert.AreEqual(TaskStatus.RanToCompletion, child.Status, "#3");
            Assert.AreEqual(TaskStatus.Canceled, inner.Status, "#4");
        }

#endif

        [Test]
        public void TaskContinuationChainLeak ()
        {
            // Start cranking out tasks, starting each new task upon completion of and from inside the prior task.
            //
            var tester = new TaskContinuationChainLeakTester ();
            tester.Run ();
            tester.TasksPilledUp.WaitOne ();

            // Head task should be out of scope by now.  Manually run the GC and expect that it gets collected.
            // 
            GC.Collect ();
            GC.WaitForPendingFinalizers ();

            try {
                // It's important that we do the asserting while the task recursion is still going, since that is the 
                // crux of the problem scenario.
                //
                tester.Verify ();
            } finally {
                tester.Stop ();
            }
        }
    }
}