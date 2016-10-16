///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using Corrade.Constants;
using wasSharp;

namespace Corrade.Structures
{
    /// <summary>
    ///     Heartbeat Structure.
    /// </summary>
    public class Heartbeat : IDisposable
    {
        /// <summary>
        ///     The number of currently executing Corrade commands.
        /// </summary>
        public int ExecutingCommands;

        /// <summary>
        ///     The number of currently executing RLV behaviours.
        /// </summary>
        public int ExecutingRLVBehaviours;

        /// <summary>
        ///     Timer computing various metrics for the heartbeat structure.
        /// </summary>
        private Time.Timer HeartbeatTimer;

        private long LastTotalCPUTicks;
        private long LastUpdateTicks;

        /// <summary>
        ///     The total number of processed Corrade commands.
        /// </summary>
        public int ProcessedCommands;

        /// <summary>
        ///     The total number of processed RLV behaviours.
        /// </summary>
        public int ProcessedRLVBehaviours;

        public Heartbeat()
        {
            // Set the Corrade version.
            Version = CORRADE_CONSTANTS.CORRADE_VERSION;

            // Compute intitial performance values.
            using (var CurrentProcess = Process.GetCurrentProcess())
            {
                StartTime = CurrentProcess.StartTime.ToUniversalTime();
                AverageCPUUsage =
                    (uint)
                        (100*CurrentProcess.TotalProcessorTime.Ticks/
                         (Environment.ProcessorCount*DateTime.UtcNow.Subtract(StartTime).Ticks));
                AverageRAMUsage = CurrentProcess.PrivateMemorySize64;

                LastTotalCPUTicks = DateTime.FromFileTimeUtc(CurrentProcess.TotalProcessorTime.Ticks).Ticks;
                LastUpdateTicks = DateTime.UtcNow.Ticks;
            }

            // Start the heartbeat timer.
            HeartbeatTimer = new Time.Timer(o =>
            {
                Heartbeats += 1;
                Uptime += 1;
                using (var CurrentProcess = Process.GetCurrentProcess())
                {
                    var totalCPUTime = DateTime.FromFileTimeUtc(CurrentProcess.TotalProcessorTime.Ticks).Ticks -
                                       LastTotalCPUTicks;
                    var updateTicks = DateTime.UtcNow.Ticks - LastUpdateTicks;
                    AverageCPUUsage = (AverageCPUUsage +
                                       (uint) ((double) totalCPUTime/updateTicks*100f/Environment.ProcessorCount))/2;
                    LastUpdateTicks = DateTime.UtcNow.Ticks;
                    LastTotalCPUTicks = DateTime.FromFileTimeUtc(CurrentProcess.TotalProcessorTime.Ticks).Ticks;

                    AverageRAMUsage = (AverageRAMUsage + CurrentProcess.PrivateMemorySize64)/2;
                }
            }, null, TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(1));
        }

        /// <summary>
        ///     The process start time.
        /// </summary>
        public DateTime StartTime { get; }

        /// <summary>
        ///     The artihmetic average of all CPU usages accross all heartbeats.
        /// </summary>
        public uint AverageCPUUsage { get; private set; }

        /// <summary>
        ///     The arithmetic average of all RAM usages across all heartbeats.
        /// </summary>
        public long AverageRAMUsage { get; private set; }

        /// <summary>
        ///     The total number of heartbeats.
        /// </summary>
        public uint Heartbeats { get; private set; }

        /// <summary>
        ///     The uptime of the current Corrade instance (updated in heartbeat intervals).
        /// </summary>
        public ulong Uptime { get; private set; }

        /// <summary>
        ///     Corrade version.
        /// </summary>
        public string Version { get; private set; }

        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        ~Heartbeat()
        {
            Dispose(false);
        }

        private void Dispose(bool dispose)
        {
            if (HeartbeatTimer != null)
            {
                HeartbeatTimer.Change(0, 0);
                HeartbeatTimer.Dispose();
                HeartbeatTimer = null;
            }
        }
    }
}