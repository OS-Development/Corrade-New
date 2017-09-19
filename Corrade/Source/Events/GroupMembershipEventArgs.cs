//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using OpenMetaverse;

namespace Corrade.Events
{
    /// <summary>
    ///     An event for the group membership notification.
    /// </summary>
    public class GroupMembershipEventArgs : EventArgs
    {
        public Enumerations.Action Action;
        public string AgentName;
        public UUID AgentUUID;
        public string GroupName;
        public UUID GroupUUID;
    }
}
