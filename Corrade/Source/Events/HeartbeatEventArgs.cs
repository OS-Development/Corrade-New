///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using OpenMetaverse;

namespace Corrade.Events
{
    /// <summary>
    ///     An event for the heartbeat notification.
    /// </summary>
    public class HeartbeatEventArgs : EventArgs
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
        ///     The total number of processed Corrade commands.
        /// </summary>
        public int ProcessedCommands;

        /// <summary>
        ///     The total number of processed RLV behaviours.
        /// </summary>
        public int ProcessedRLVBehaviours;

        /// <summary>
        ///     The artihmetic average of all CPU usages accross all heartbeats.
        /// </summary>
        public uint AverageCPUUsage;

        /// <summary>
        ///     The arithmetic average of all RAM usages across all heartbeats.
        /// </summary>
        public long AverageRAMUsage;

        /// <summary>
        ///     The total number of heartbeats.
        /// </summary>
        public uint Heartbeats;

        /// <summary>
        ///     The uptime of the current Corrade instance (updated in heartbeat intervals).
        /// </summary>
        public ulong Uptime;

        /// <summary>
        ///     Corrade version.
        /// </summary>
        public string Version;
    }
}