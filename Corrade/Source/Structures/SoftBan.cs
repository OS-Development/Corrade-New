///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using OpenMetaverse;
using wasSharp;

namespace Corrade.Structures
{
    /// <summary>
    ///     Soft ban structure.
    /// </summary>
    public struct SoftBan
    {
        /// <summary>
        ///     The agent UUID.
        /// </summary>
        [Reflection.NameAttribute("agent")] public UUID Agent;

        [Reflection.NameAttribute("firstname")] public string FirstName;
        [Reflection.NameAttribute("lastname")] public string LastName;

        /// <summary>
        ///     Hard time measured in minutes.
        /// </summary>
        [Reflection.NameAttribute("time")] public ulong Time;

        /// <summary>
        ///     Optional notes for the ban.
        /// </summary>
        [Reflection.NameAttribute("note")] public string Note;

        /// <summary>
        ///     The time span when the ban was placed originally.
        /// </summary>
        [Reflection.NameAttribute("timestamp")] public string Timestamp;

        /// <summary>
        ///     The last time the agent attempted to join the group and got banned.
        /// </summary>
        [Reflection.NameAttribute("last")] public string Last;
    }
}