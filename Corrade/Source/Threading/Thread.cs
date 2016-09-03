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
        ///     Holds all the live threads.
        /// </summary>
        private static readonly HashSet<System.Threading.Thread> WorkSet = new HashSet<System.Threading.Thread>();

        private static readonly object WorkSetLock = new object();

        private static readonly Random corradeRandom = new Random();

        /// <summary>
        ///     Semaphore for sequential execution of threads.
        /// </summary>
        private static readonly ManualResetEvent SequentialThreadCompletedEvent = new ManualResetEvent(true);

        /// <summary>
        ///     Holds a map of groups to execution time in milliseconds.
        /// </summary>
        private static Dictionary<UUID, GroupExecution> GroupExecutionTime =
            new Dictionary<UUID, GroupExecution>();

        private static readonly object GroupExecutionTimeLock = new object();
        private static readonly Stopwatch ThreadExecutuionStopwatch = new Stopwatch();
        private readonly Enumerations.ThreadType threadType;

        /// <summary>
        ///     Constructor for a Corrade thread.
        /// </summary>
        /// <param name="threadType">the type of Corrade thread</param>
        public Thread(Enumerations.ThreadType threadType)
        {
            this.threadType = threadType;
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
            lock (WorkSetLock)
            {
                if (WorkSet.Count > m)
                {
                    return;
                }
            }
            var threadType = this.threadType;
            System.Threading.Thread t = null;
            t = new System.Threading.Thread(() =>
            {
                // Wait for previous sequential thread to complete.
                SequentialThreadCompletedEvent.WaitOne((int) millisecondsTimeout, false);
                SequentialThreadCompletedEvent.Reset();
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
                        Reflection.GetNameFromEnumValue(threadType), ex.Message, ex.InnerException?.Message);
                }
                // Thread has completed.
                SequentialThreadCompletedEvent.Set();
                lock (WorkSetLock)
                {
                    WorkSet.Remove(t);
                }
            })
            {IsBackground = true};
            lock (WorkSetLock)
            {
                WorkSet.Add(t);
            }
            t.Start();
        }

        /// <summary>
        ///     This is an ad-hoc scheduler where threads will be executed in a
        ///     first-come first-served fashion.
        /// </summary>
        /// <param name="s">the code to execute as a ThreadStart delegate</param>
        /// <param name="m">the maximum amount of threads</param>
        public void Spawn(ThreadStart s, uint m)
        {
            lock (WorkSetLock)
            {
                if (WorkSet.Count > m)
                {
                    return;
                }
            }
            var threadType = this.threadType;
            System.Threading.Thread t = null;
            t = new System.Threading.Thread(() =>
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
                        Reflection.GetNameFromEnumValue(threadType), ex.Message, ex.InnerException?.Message);
                }
                lock (WorkSetLock)
                {
                    WorkSet.Remove(t);
                }
            })
            {IsBackground = true};
            lock (WorkSetLock)
            {
                WorkSet.Add(t);
            }
            t.Start();
        }

        /// <summary>
        ///     This is an blocking scheduler where threads will be waited upon.
        /// </summary>
        /// <param name="s">the code to execute as a ThreadStart delegate</param>
        /// <param name="m">the maximum amount of threads</param>
        /// <param name="millisecondsTimeout">the timout after which to abort the thread</param>
        public void SpawnBlock(ThreadStart s, uint m, uint millisecondsTimeout)
        {
            lock (WorkSetLock)
            {
                if (WorkSet.Count > m)
                {
                    return;
                }
            }
            var threadType = this.threadType;
            System.Threading.Thread t = null;
            t = new System.Threading.Thread(() =>
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
                        Reflection.GetNameFromEnumValue(threadType), ex.Message, ex.InnerException?.Message);
                }
                lock (WorkSetLock)
                {
                    WorkSet.Remove(t);
                }
            })
            {IsBackground = true};
            lock (WorkSetLock)
            {
                WorkSet.Add(t);
            }
            t.Start();
            // Now block until return.
            if (
                t.ThreadState.Equals(ThreadState.Running) ||
                t.ThreadState.Equals(ThreadState.WaitSleepJoin))
            {
                if (t.Join((int) millisecondsTimeout))
                {
                    return;
                }
                // Timeout elapsed, so we force an abort and remove the thread from the workset.
                try
                {
                    t.Abort();
                    t.Join();
                }
                catch (Exception)
                {
                    lock (WorkSetLock)
                    {
                        WorkSet.Remove(t);
                    }
                }
            }
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
            lock (WorkSetLock)
            {
                if (WorkSet.Count > m)
                {
                    return;
                }
            }
            var threadType = this.threadType;
            System.Threading.Thread t = null;
            t = new System.Threading.Thread(() =>
            {
                // protect inner thread
                try
                {
                    // First remove any groups that have expired.
                    lock (GroupExecutionTimeLock)
                    {
                        GroupExecutionTime =
                            GroupExecutionTime.AsParallel().Where(
                                o => (DateTime.Now - o.Value.TimeStamp).Milliseconds < expiration)
                                .ToDictionary(o => o.Key, o => o.Value);
                    }
                    var sleepTime = 0;
                    var sortedTimeGroups = new List<int>();
                    lock (GroupExecutionTimeLock)
                    {
                        // In case only one group is involved, then do not schedule the group.
                        if (GroupExecutionTime.Count > 1 && GroupExecutionTime.ContainsKey(groupUUID))
                        {
                            sortedTimeGroups.AddRange(
                                GroupExecutionTime.OrderBy(o => o.Value.ExecutionTime)
                                    .Select(o => o.Value.ExecutionTime));
                        }
                    }
                    switch (sortedTimeGroups.Any())
                    {
                        case true:
                            var draw = corradeRandom.Next(sortedTimeGroups.Sum(o => o));
                            var accu = 0;
                            foreach (var time in sortedTimeGroups)
                            {
                                accu += time;
                                if (accu < draw) continue;
                                sleepTime = time;
                                break;
                            }
                            break;
                    }
                    System.Threading.Thread.Sleep(sleepTime);
                    ThreadExecutuionStopwatch.Restart();
                    s();
                    ThreadExecutuionStopwatch.Stop();
                    lock (GroupExecutionTimeLock)
                    {
                        // add or change the mean execution time for a group
                        switch (GroupExecutionTime.ContainsKey(groupUUID))
                        {
                            case true:
                                GroupExecutionTime[groupUUID] = new GroupExecution
                                {
                                    ExecutionTime = (GroupExecutionTime[groupUUID].ExecutionTime +
                                                     (int) ThreadExecutuionStopwatch.ElapsedMilliseconds)/
                                                    2,
                                    TimeStamp = DateTime.Now
                                };
                                break;
                            default:
                                GroupExecutionTime.Add(groupUUID, new GroupExecution
                                {
                                    ExecutionTime = (int) ThreadExecutuionStopwatch.ElapsedMilliseconds,
                                    TimeStamp = DateTime.Now
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
                lock (WorkSetLock)
                {
                    WorkSet.Remove(t);
                }
            })
            {IsBackground = true};
            lock (WorkSetLock)
            {
                WorkSet.Add(t);
            }
            t.Start();
        }

        private struct GroupExecution
        {
            public int ExecutionTime;
            public DateTime TimeStamp;
        }
    }
}