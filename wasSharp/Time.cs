///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace wasSharp
{
    public class Time
    {
        public delegate void TimerCallback(object state);

        /// <summary>
        ///     Convert an Unix timestamp to a DateTime structure.
        /// </summary>
        /// <param name="unixTimestamp">the Unix timestamp to convert</param>
        /// <returns>the DateTime structure</returns>
        /// <remarks>the function assumes UTC time</remarks>
        public static DateTime UnixTimestampToDateTime(uint unixTimestamp)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTimestamp).ToUniversalTime();
        }

        /// <summary>
        ///     Convert a DateTime structure to a Unix timestamp.
        /// </summary>
        /// <param name="dateTime">the DateTime structure to convert</param>
        /// <returns>the Unix timestamp</returns>
        /// <remarks>the function assumes UTC time</remarks>
        public static uint DateTimeToUnixTimestamp(DateTime dateTime)
        {
            return (uint) (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
        }

        public sealed class Timer : IDisposable
        {
            private static readonly Task CompletedTask = Task.FromResult(false);
            private readonly TimerCallback Callback;
            private readonly object State;
            private Task Delay;
            private bool Disposed;
            private int Period;
            private CancellationTokenSource TokenSource;

            public Timer(TimerCallback callback, object state, int dueTime, int period)
            {
                Callback = callback;
                State = state;
                Period = period;
                Reset(dueTime);
            }

            public Timer(TimerCallback callback, object state, TimeSpan dueTime, TimeSpan period)
                : this(callback, state, (int) dueTime.TotalMilliseconds, (int) period.TotalMilliseconds)
            {
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            ~Timer()
            {
                Dispose(false);
            }

            private void Dispose(bool cleanUpManagedObjects)
            {
                if (cleanUpManagedObjects)
                    Cancel();
                Disposed = true;
            }

            public void Change(int dueTime, int period)
            {
                Period = period;
                Reset(dueTime);
            }

            public void Change(TimeSpan dueTime, TimeSpan period)
            {
                Change((int) dueTime.TotalMilliseconds, (int) period.TotalMilliseconds);
            }

            private void Reset(int due)
            {
                Cancel();
                if (due >= 0)
                {
                    TokenSource = new CancellationTokenSource();
                    Action tick = null;
                    tick = () =>
                    {
                        Task.Run(() => Callback(State));
                        if (Disposed || Period < 0) return;
                        Delay = Period > 0 ? Task.Delay(Period, TokenSource.Token) : CompletedTask;
                        Delay.ContinueWith(t => tick(), TokenSource.Token);
                    };
                    Delay = due > 0 ? Task.Delay(due, TokenSource.Token) : CompletedTask;
                    Delay.ContinueWith(t => tick(), TokenSource.Token);
                }
            }

            private void Cancel()
            {
                if (TokenSource != null)
                {
                    TokenSource.Cancel();
                    TokenSource.Dispose();
                    TokenSource = null;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2015 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Given a number of allowed events per seconds, this class allows you
        ///     to determine via the IsSafe property whether it is safe to trigger
        ///     another lined-up event. This is mostly used to check that throttles
        ///     are being respected.
        /// </summary>
        public class TimedThrottle : IDisposable
        {
            private readonly uint EventsAllowed;
            private readonly object LockObject = new object();
            private Timer timer;
            private uint TriggeredEvents;

            public TimedThrottle(uint events, uint seconds)
            {
                EventsAllowed = events;
                if (timer == null)
                {
                    timer = new Timer(o =>
                    {
                        lock (LockObject)
                        {
                            TriggeredEvents = 0;
                        }
                    }, null, (int) seconds, (int) seconds);
                }
            }

            public bool IsSafe
            {
                get
                {
                    lock (LockObject)
                    {
                        return ++TriggeredEvents <= EventsAllowed;
                    }
                }
            }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            protected virtual void Dispose(bool dispose)
            {
                if (timer != null)
                {
                    timer.Dispose();
                    timer = null;
                }
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     An alarm class similar to the UNIX alarm with the added benefit
        ///     of a decaying timer that tracks the time between rescheduling.
        /// </summary>
        /// <remarks>
        ///     (C) Wizardry and Steamworks 2013 - License: GNU GPLv3
        /// </remarks>
        public class DecayingAlarm : IDisposable
        {
            [Flags]
            public enum DECAY_TYPE
            {
                [XmlEnum(Name = "none")] NONE = 0,
                [XmlEnum(Name = "arithmetic")] ARITHMETIC = 1,
                [XmlEnum(Name = "geometric")] GEOMETRIC = 2,
                [XmlEnum(Name = "harmonic")] HARMONIC = 4,
                [XmlEnum(Name = "weighted")] WEIGHTED = 5
            }

            private readonly DECAY_TYPE decay = DECAY_TYPE.NONE;
            private readonly Stopwatch elapsed = new Stopwatch();
            private readonly object LockObject = new object();
            private readonly HashSet<double> times = new HashSet<double>();
            private Timer alarm;

            /// <summary>
            ///     The default constructor using no decay.
            /// </summary>
            public DecayingAlarm()
            {
                Signal = new ManualResetEvent(false);
            }

            /// <summary>
            ///     The constructor for the DecayingAlarm class taking as parameter a decay type.
            /// </summary>
            /// <param name="decay">the type of decay: arithmetic, geometric, harmonic, heronian or quadratic</param>
            public DecayingAlarm(DECAY_TYPE decay)
            {
                Signal = new ManualResetEvent(false);
                this.decay = decay;
            }

            public ManualResetEvent Signal { get; set; }

            public void Dispose()
            {
                Dispose(true);
                GC.SuppressFinalize(this);
            }

            public void Alarm(double deadline)
            {
                lock (LockObject)
                {
                    switch (alarm == null)
                    {
                        case true:
                            elapsed.Start();
                            alarm = new Timer(o =>
                            {
                                lock (LockObject)
                                {
                                    Signal.Set();
                                    elapsed.Stop();
                                    times.Clear();
                                    alarm.Dispose();
                                    alarm = null;
                                }
                            }, null, (int) deadline, 0);
                            return;
                        case false:
                            elapsed.Stop();
                            times.Add(elapsed.ElapsedMilliseconds);
                            switch (decay)
                            {
                                case DECAY_TYPE.ARITHMETIC:
                                    alarm?.Change(
                                        (int) ((deadline + times.Aggregate((a, b) => b + a))/(1f + times.Count)), 0);
                                    break;
                                case DECAY_TYPE.GEOMETRIC:
                                    alarm?.Change((int) (Math.Pow(deadline*times.Aggregate((a, b) => b*a),
                                        1f/(1f + times.Count))), 0);
                                    break;
                                case DECAY_TYPE.HARMONIC:
                                    alarm?.Change((int) ((1f + times.Count)/
                                                         (1f/deadline + times.Aggregate((a, b) => 1f/b + 1f/a))), 0);
                                    break;
                                case DECAY_TYPE.WEIGHTED:
                                    HashSet<double> d = new HashSet<double>(times) {deadline};
                                    double total = d.Aggregate((a, b) => b + a);
                                    alarm?.Change(
                                        (int) (d.Aggregate((a, b) => Math.Pow(a, 2)/total + Math.Pow(b, 2)/total)), 0);
                                    break;
                                default:
                                    alarm?.Change((int) deadline, 0);
                                    break;
                            }
                            elapsed.Reset();
                            elapsed.Start();
                            break;
                    }
                }
            }

            protected virtual void Dispose(bool dispose)
            {
                if (alarm != null)
                {
                    alarm.Dispose();
                    alarm = null;
                }
            }
        }
    }
}