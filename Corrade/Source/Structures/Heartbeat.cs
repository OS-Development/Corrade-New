///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Diagnostics;
using Corrade.Constants;
using wasSharp;
using wasSharpNET;

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

        private PerformanceCounter CPUCounter;
        private PerformanceCounter RAMCounter;

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

            // Get instance name and start time.
            string processInstanceName;
            using (var process = System.Diagnostics.Process.GetCurrentProcess())
            {
                processInstanceName = process.GetProcessInstanceName();
                StartTime = process.StartTime;
            }

            // Initialize the performance counters.
            CPUCounter = new PerformanceCounter("Process", "% Processor Time", processInstanceName, true);
            AverageCPUUsage = (uint) CPUCounter.NextValue();
            RAMCounter = new PerformanceCounter("Process", "Working Set", processInstanceName, true);
            AverageRAMUsage = (uint) RAMCounter.NextValue();

            // Start the heartbeat timer.
            HeartbeatTimer = new Time.Timer(o =>
            {
                Heartbeats += 1;
                Uptime += 1;
                AverageCPUUsage = (AverageCPUUsage + (uint) CPUCounter.NextValue())/2;
                AverageRAMUsage = (AverageRAMUsage + (uint) RAMCounter.NextValue())/2;
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
            if (CPUCounter != null)
            {
                CPUCounter.Dispose();
                CPUCounter = null;
            }
            if (RAMCounter != null)
            {
                RAMCounter.Dispose();
                RAMCounter = null;
            }
        }
    }
}