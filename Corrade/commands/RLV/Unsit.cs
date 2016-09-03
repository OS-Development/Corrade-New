///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using OpenMetaverse;
using wasOpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> unsit = (message, rule, senderUUID) =>
            {
                if (!rule.Param.Equals(wasOpenMetaverse.RLV.RLV_CONSTANTS.FORCE))
                {
                    return;
                }
                if (Client.Self.Movement.SitOnGround || !Client.Self.SittingOn.Equals(0))
                {
                    Client.Self.Stand();
                }
                // stop all non-built-in animations
                lock (Locks.ClientInstanceSelfLock)
                {
                    Client.Self.SignaledAnimations.Copy()
                        .Keys.AsParallel()
                        .Where(o => !wasOpenMetaverse.Helpers.LindenAnimations.Contains(o))
                        .ForAll(o => { Client.Self.AnimationStop(o, true); });
                }
                // Set the camera on the avatar.
                lock (Locks.ClientInstanceSelfLock)
                {
                    Client.Self.Movement.Camera.LookAt(
                        Client.Self.SimPosition,
                        Client.Self.SimPosition
                        );
                }
            };
        }
    }
}