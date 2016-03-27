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
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Helpers = wasOpenMetaverse.Helpers;
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
                            (int) Configuration.Permissions.Movement))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Vector3 offset;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.OFFSET)),
                                    corradeCommandParameters.Message)),
                            out offset))
                    {
                        offset = Vector3.Zero;
                    }
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)),
                            out range))
                    {
                        range = corradeConfiguration.Range;
                    }
                    Primitive primitive = null;
                    if (
                        !Services.FindPrimitive(Client,
                            Helpers.StringOrUUID(wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                                corradeCommandParameters.Message))),
                            range, corradeConfiguration.Range,
                            ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
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
                        if (args.Message.Equals(Constants.ALERTS.NO_ROOM_TO_SIT_HERE))
                        {
                            succeeded = false;
                        }
                        SitEvent.Set();
                    };
                    if (Client.Self.Movement.SitOnGround || !Client.Self.SittingOn.Equals(0))
                    {
                        Client.Self.Stand();
                    }
                    // stop non default animations if requested
                    bool deanimate;
                    switch (bool.TryParse(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DEANIMATE)),
                            corradeCommandParameters.Message)), out deanimate) && deanimate)
                    {
                        case true:
                            // stop all non-built-in animations
                            HashSet<UUID> lindenAnimations = new HashSet<UUID>(typeof (Animations).GetFields(
                                BindingFlags.Public |
                                BindingFlags.Static).AsParallel().Select(o => (UUID) o.GetValue(null)));
                            Parallel.ForEach(
                                Client.Self.SignaledAnimations.Copy()
                                    .Keys.AsParallel()
                                    .Where(o => !lindenAnimations.Contains(o)),
                                o => { Client.Self.AnimationStop(o, true); });
                            break;
                    }
                    lock (Locks.ClientInstanceSelfLock)
                    {
                        Client.Self.AvatarSitResponse += AvatarSitEventHandler;
                        Client.Self.AlertMessage += AlertMessageEventHandler;
                        Client.Self.RequestSit(primitive.ID, offset);
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