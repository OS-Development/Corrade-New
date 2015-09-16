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
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> sit =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Permissions.Movement))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)),
                            out range))
                    {
                        range = corradeConfiguration.Range;
                    }
                    Primitive primitive = null;
                    if (
                        !FindPrimitive(
                            StringOrUUID(wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)),
                                corradeCommandParameters.Message))),
                            range,
                            ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                    {
                        throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                    }
                    ManualResetEvent SitEvent = new ManualResetEvent(false);
                    bool succeeded = false;
                    EventHandler<AvatarSitResponseEventArgs> AvatarSitEventHandler = (sender, args) =>
                    {
                        succeeded = !args.ObjectID.Equals(UUID.Zero);
                        SitEvent.Set();
                    };
                    EventHandler<AlertMessageEventArgs> AlertMessageEventHandler = (sender, args) =>
                    {
                        if (args.Message.Equals(LINDEN_CONSTANTS.ALERTS.NO_ROOM_TO_SIT_HERE))
                        {
                            succeeded = false;
                        }
                        SitEvent.Set();
                    };
                    if (Client.Self.Movement.SitOnGround || !Client.Self.SittingOn.Equals(0))
                    {
                        Client.Self.Stand();
                    }
                    // stop all non-built-in animations
                    List<UUID> lindenAnimations = new List<UUID>(typeof (Animations).GetProperties(
                        BindingFlags.Public |
                        BindingFlags.Static).AsParallel().Select(o => (UUID) o.GetValue(null)).ToList());
                    Parallel.ForEach(Client.Self.SignaledAnimations.Copy().Keys, o =>
                    {
                        if (!lindenAnimations.Contains(o))
                            Client.Self.AnimationStop(o, true);
                    });
                    lock (ClientInstanceSelfLock)
                    {
                        Client.Self.AvatarSitResponse += AvatarSitEventHandler;
                        Client.Self.AlertMessage += AlertMessageEventHandler;
                        Client.Self.RequestSit(primitive.ID, Vector3.Zero);
                        if (!SitEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Self.AvatarSitResponse -= AvatarSitEventHandler;
                            Client.Self.AlertMessage -= AlertMessageEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_REQUESTING_SIT);
                        }
                        Client.Self.AvatarSitResponse -= AvatarSitEventHandler;
                        Client.Self.AlertMessage -= AlertMessageEventHandler;
                    }
                    if (!succeeded)
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_SIT);
                    }
                    Client.Self.Sit();
                    // Set the camera on the avatar.
                    Client.Self.Movement.Camera.LookAt(
                        Client.Self.SimPosition,
                        Client.Self.SimPosition
                        );
                };
        }
    }
}