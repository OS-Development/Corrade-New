///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> sit = (message, rule, senderUUID) =>
            {
                UUID sitTarget;
                if (!rule.Param.Equals(RLV_CONSTANTS.FORCE) || !UUID.TryParse(rule.Option, out sitTarget) ||
                    sitTarget.Equals(UUID.Zero))
                {
                    return;
                }
                Primitive primitive = null;
                if (
                    !FindPrimitive(sitTarget,
                        LINDEN_CONSTANTS.LSL.SENSOR_RANGE,
                        ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                {
                    return;
                }
                ManualResetEvent SitEvent = new ManualResetEvent(false);
                EventHandler<AvatarSitResponseEventArgs> AvatarSitEventHandler =
                    (sender, args) =>
                        SitEvent.Set();
                EventHandler<AlertMessageEventArgs> AlertMessageEventHandler = (sender, args) => SitEvent.Set();
                if (Client.Self.Movement.SitOnGround || !Client.Self.SittingOn.Equals(0))
                {
                    Client.Self.Stand();
                }
                // stop all non-built-in animations
                HashSet<UUID> lindenAnimations = new HashSet<UUID>(typeof (Animations).GetFields(
                    BindingFlags.Public |
                    BindingFlags.Static).AsParallel().Select(o => (UUID) o.GetValue(null)));
                Parallel.ForEach(
                    Client.Self.SignaledAnimations.Copy()
                        .Keys.AsParallel()
                        .Where(o => !lindenAnimations.Contains(o)),
                    o => { Client.Self.AnimationStop(o, true); });
                lock (ClientInstanceSelfLock)
                {
                    Client.Self.AvatarSitResponse += AvatarSitEventHandler;
                    Client.Self.AlertMessage += AlertMessageEventHandler;
                    Client.Self.RequestSit(primitive.ID, Vector3.Zero);
                    SitEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false);
                    Client.Self.AvatarSitResponse -= AvatarSitEventHandler;
                    Client.Self.AlertMessage -= AlertMessageEventHandler;
                }
                // Set the camera on the avatar.
                Client.Self.Movement.Camera.LookAt(
                    Client.Self.SimPosition,
                    Client.Self.SimPosition
                    );
            };
        }
    }
}