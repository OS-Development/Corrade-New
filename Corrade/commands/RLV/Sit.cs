///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using OpenMetaverse;
using System;
using System.Linq;
using System.Threading;
using wasOpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static readonly Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> sit =
                (message, rule, senderUUID) =>
                {
                    UUID sitTarget;
                    if (!rule.Param.Equals(wasOpenMetaverse.RLV.RLV_CONSTANTS.FORCE) ||
                        !UUID.TryParse(rule.Option, out sitTarget) ||
                        sitTarget.Equals(UUID.Zero))
                    {
                        return;
                    }
                    Primitive primitive = null;
                    if (
                        !Services.FindPrimitive(Client, sitTarget,
                            wasOpenMetaverse.Constants.LSL.SENSOR_RANGE,
                            ref primitive, corradeConfiguration.DataTimeout))
                    {
                        return;
                    }
                    var SitEvent = new ManualResetEvent(false);
                    EventHandler<AvatarSitResponseEventArgs> AvatarSitEventHandler =
                        (sender, args) =>
                            SitEvent.Set();
                    EventHandler<AlertMessageEventArgs> AlertMessageEventHandler = (sender, args) => SitEvent.Set();
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    if (Client.Self.Movement.SitOnGround || !Client.Self.SittingOn.Equals(0))
                    {
                        Client.Self.Stand();
                    }
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                    // stop all non-built-in animations
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.SignaledAnimations.Copy()
                            .Keys.AsParallel()
                            .Where(o => !wasOpenMetaverse.Helpers.LindenAnimations.Contains(o))
                            .ForAll(o => { Client.Self.AnimationStop(o, true); });
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.AvatarSitResponse += AvatarSitEventHandler;
                    Client.Self.AlertMessage += AlertMessageEventHandler;
                    Client.Self.RequestSit(primitive.ID, Vector3.Zero);
                    SitEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, true);
                    Client.Self.AvatarSitResponse -= AvatarSitEventHandler;
                    Client.Self.AlertMessage -= AlertMessageEventHandler;
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                    // Set the camera on the avatar.
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.Movement.Camera.LookAt(
                            Client.Self.SimPosition,
                            Client.Self.SimPosition
                            );
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                };
        }
    }
}
