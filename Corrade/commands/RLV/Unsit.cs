///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> unsit = (message, rule, senderUUID) =>
            {
                if (!rule.Param.Equals(RLV_CONSTANTS.FORCE))
                {
                    return;
                }
                if (Client.Self.Movement.SitOnGround || !Client.Self.SittingOn.Equals(0))
                {
                    Client.Self.Stand();
                }
                // stop all non-built-in animations
                HashSet<UUID> lindenAnimations = new HashSet<UUID>(typeof (Animations).GetFields(
                    BindingFlags.Public |
                    BindingFlags.Static).AsParallel().Select(o => (UUID) o.GetValue(null)));
                Client.Self.SignaledAnimations.Copy()
                    .Keys.AsParallel()
                    .Where(o => !lindenAnimations.Contains(o)).ForAll(o => { Client.Self.AnimationStop(o, true); });
                // Set the camera on the avatar.
                Client.Self.Movement.Camera.LookAt(
                    Client.Self.SimPosition,
                    Client.Self.SimPosition
                    );
            };
        }
    }
}