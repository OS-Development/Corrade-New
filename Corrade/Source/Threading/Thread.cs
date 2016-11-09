///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using Amib.Threading;
using OpenMetaverse;
using wasSharp;
using ThreadState = System.Threading.ThreadState;

namespace Corrade.Threading
{
    /// <summary>
    ///     Corrade's internal thread Structure.
    /// </summary>
    public class Thread
    {
        /// <summary>
        ///     Thread pool.
        /// </summary>
        private static readonly SmartThreadPool smartThreadPool = new SmartThreadPool();
        public Collections.RangeCollection<WorkItemPriority> threadRangePriority = new Collections.RangeCollection<WorkItemPriority>(0, 100);

        /// <summary>
        ///     Semaphore for sequential execution of threads.
        /// </summary>
        private static readonly ManualResetEvent SequentialThreadCompletedEvent = new ManualResetEvent(true);

        /// <summary>
        ///     Holds group execution times.
        /// </summary>
        private static SortedSet<GroupExecution> GroupExecutionSet = new SortedSet<GroupExecution>();
        private static readonly object GroupExecutionSetLock = new object();
        private static readonly Stopwatch ThreadExecutuionStopwatch = new Stopwatch();
        private readonly Enumerations.ThreadType threadType;

        /// <summary>
        ///     Constructor for a Corrade thread.
        /// </summary>
        /// <param name="threadType">the type of Corrade thread</param>
        public Thread(Enumerations.ThreadType threadType)
        {
            // Get the thread type.
            this.threadType = threadType;

            // Set priority ranges.
            threadRangePriority.Add(WorkItemPriority.Highest, 0, 20);
            threadRangePriority.Add(WorkItemPriority.AboveNormal, 21, 40);
            threadRangePriority.Add(WorkItemPriority.Normal, 41, 60);
            threadRangePriority.Add(WorkItemPriority.BelowNormal, 61, 80);
            threadRangePriority.Add(WorkItemPriority.Lowest, 81, 100);
        }

        /// <summary>
        ///     This is a sequential scheduler that benefits from not blocking Corrade
        ///     and guarrantees that any Corrade thread spawned this way will only execute
        ///     until the previous thread spawned this way has completed.
        /// </summary>
        /// <param name="s">the code to execute as a ThreadStart delegate</param>
        /// <param name="m">the maximum amount of threads</param>
        /// <param name="millisecondsTimeout">
        ///     the timeout in milliseconds before considering the previous thread as vanished
        /// </param>
        public void SpawnSequential(ThreadStart s, uint m, uint millisecondsTimeout)
        {
            if (smartThreadPool.InUseThreads > m)
                return;

            var threadType = this.threadType;
            smartThreadPool.QueueWorkItem(() =>
            {
                // protect inner thread
                try
                {
                    SequentialThreadCompletedEvent.WaitOne((int)millisecondsTimeout, false);
                    SequentialThreadCompletedEvent.Reset();
                    s();
                    SequentialThreadCompletedEvent.Set();
                }
                catch (Exception ex)
                {
                    Corrade.Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            global::Corrade.Enumerations.ConsoleMessage.UNCAUGHT_EXCEPTION_FOR_THREAD),
                        Reflection.GetNameFromEnumValue(threadType), ex.Message, ex.InnerException?.Message);
                }
            });
        }

        /// <summary>
        ///     This is an ad-hoc scheduler where threads will be executed in a
        ///     first-come first-served fashion.
        /// </summary>
        /// <param name="s">the code to execute as a ThreadStart delegate</param>
        /// <param name="m">the maximum amount of threads</param>
        public void Spawn(ThreadStart s, uint m)
        {
            if (smartThreadPool.InUseThreads > m)
                return;

            var threadType = this.threadType;
            smartThreadPool.QueueWorkItem(() =>
            {
                // protect inner thread
                try
                {
                    s();
                }
                catch (Exception ex)
                {
                    Corrade.Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            global::Corrade.Enumerations.ConsoleMessage.UNCAUGHT_EXCEPTION_FOR_THREAD),
                        Reflection.GetNameFromEnumValue(threadType), ex.Message, ex.InnerException?.Message,
                        ex.StackTrace);
                }
            });
        }

        /// <summary>
        ///     This is a fairness-oriented group/time-based scheduler that monitors
        ///     the execution time of threads for each configured group and favors
        ///     threads for the configured groups that have the smallest accumulated
        ///     execution time.
        /// </summary>
        /// <param name="s">the code to execute as a ThreadStart delegate</param>
        /// <param name="m">the maximum amount of threads</param>
        /// <param name="groupUUID">the UUID of the group</param>
        /// <param name="expiration">the time in milliseconds after which measurements are expunged</param>
        public void Spawn(ThreadStart s, uint m, UUID groupUUID, uint expiration)
        {
            // Don't accept to schedule bogus groups.
            if (groupUUID.Equals(UUID.Zero))
                return;

            if (smartThreadPool.InUseThreads > m)
                return;

            WorkItemPriority workItemPriority = WorkItemPriority.Normal;
            lock (GroupExecutionSetLock)
            {
                // Clear threads that are not restricted anymore due to expiration.
                GroupExecutionSet.RemoveWhere(o => (DateTime.UtcNow - o.TimeStamp).Milliseconds > expiration);

                var groupExecution = GroupExecutionSet.FirstOrDefault(o => o.GroupUUID.Equals(groupUUID));
                // Adjust the priority depending on the time spent executing a command.
                if (GroupExecutionSet.Some() && !groupExecution.Equals(default(GroupExecution)))
                    workItemPriority = threadRangePriority[(int)(100L * groupExecution.ExecutionTime / GroupExecutionSet.Sum(o => o.ExecutionTime))];
            }

            // Spawn.
            var threadType = this.threadType;
            smartThreadPool.QueueWorkItem(() =>
            {
                // protect inner thread
                try
                {
                    ThreadExecutuionStopwatch.Restart();
                    s();
                    ThreadExecutuionStopwatch.Stop();
                    lock (GroupExecutionSetLock)
                    {
                        // add or change the mean execution time for a group
                        var groupExecution = GroupExecutionSet.FirstOrDefault(o => o.GroupUUID.Equals(groupUUID));
                        switch (!groupExecution.Equals(default(GroupExecution)))
                        {
                            case true:
                                GroupExecutionSet.Remove(groupExecution);
                                groupExecution.ExecutionTime = (groupExecution.ExecutionTime + ThreadExecutuionStopwatch.ElapsedMilliseconds) / 2;
                                groupExecution.TimeStamp = DateTime.UtcNow;
                                GroupExecutionSet.Add(groupExecution);
                                break;
                            default:
                                GroupExecutionSet.Add(new GroupExecution
                                {
                                    GroupUUID = groupUUID,
                                    ExecutionTime = ThreadExecutuionStopwatch.ElapsedMilliseconds,
                                    TimeStamp = DateTime.UtcNow
                                });
                                break;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Corrade.Feedback(
                        Reflection.GetDescriptionFromEnumValue(
                            global::Corrade.Enumerations.ConsoleMessage.UNCAUGHT_EXCEPTION_FOR_THREAD),
                        Reflection.GetNameFromEnumValue(threadType), ex.Message, ex.InnerException?.Message,
                        ex.StackTrace);
                }
            }, workItemPriority);
        }

        private struct GroupExecution : IComparer<GroupExecution>, IComparable<GroupExecution>
        {
            public UUID GroupUUID;
            public long ExecutionTime;
            public DateTime TimeStamp;

            int IComparer<GroupExecution>.Compare(GroupExecution x, GroupExecution y)
            {
                if (x.ExecutionTime.Equals(y.ExecutionTime))
                    return 0;
                if (x.ExecutionTime < y.ExecutionTime)
                    return -1;

                return 1;
            }

            int IComparable<GroupExecution>.CompareTo(GroupExecution o)
            {
                return ExecutionTime.CompareTo(o.ExecutionTime);
            }
        }
    }
}