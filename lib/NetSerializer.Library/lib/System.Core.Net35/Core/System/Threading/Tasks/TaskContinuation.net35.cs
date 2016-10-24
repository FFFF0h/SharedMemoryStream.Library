﻿#if NET35

using System.Diagnostics.Contracts;

namespace System.Threading.Tasks
{
    internal abstract class TaskContinuation
    {
        internal abstract void Run(Task completedTask, bool canInlineContinuationTask);

        protected static void InlineIfPossibleOrElseQueue(Task task)
        {
            Contract.Requires(task != null);
            var scheduler = task.Scheduler;
            if (scheduler == null)
            {
                Contract.Assert(false);
                throw new InvalidOperationException();
            }
            task.Start(task.Scheduler, true, false);
        }
    }
}

#endif