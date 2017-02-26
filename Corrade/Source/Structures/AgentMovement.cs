///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using String = wasSharp.String;
using OpenMetaverse;

namespace Corrade.Structures
{
    /// <summary>
    ///     A structure for the agent movement.
    /// </summary>
    [Serializable]
    public struct AgentMovement
    {
        public bool AlwaysRun;
        public bool AutoResetControls;
        public bool Away;
        public Quaternion BodyRotation;
        public AgentFlags Flags;
        public bool Fly;
        public Quaternion HeadRotation;
        public bool Mouselook;
        public bool SitOnGround;
        public bool StandUp;
        public AgentState State;
    }
}