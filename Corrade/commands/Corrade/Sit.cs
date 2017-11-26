///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> sit =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Movement))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    Vector3 offset;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.OFFSET)),
                                    corradeCommandParameters.Message)),
                            out offset))
                        offset = Vector3.Zero;
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                            out range))
                        range = corradeConfiguration.Range;
                    Primitive primitive = null;
                    var item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                    UUID itemUUID;
                    switch (UUID.TryParse(item, out itemUUID))
                    {
                        case true:
                            if (
                                !Services.FindPrimitive(Client,
                                    itemUUID,
                                    range,
                                    ref primitive,
                                    corradeConfiguration.DataTimeout))
                                throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                            break;

                        default:
                            if (
                                !Services.FindPrimitive(Client,
                                    item,
                                    range,
                                    ref primitive,
                                    corradeConfiguration.DataTimeout))
                                throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                            break;
                    }
                    var SitEvent = new ManualResetEventSlim(false);
                    var succeeded = false;
                    EventHandler<AvatarSitResponseEventArgs> AvatarSitEventHandler = (sender, args) =>
                    {
                        succeeded = !args.ObjectID.Equals(UUID.Zero);
                        SitEvent.Set();
                    };
                    EventHandler<AlertMessageEventArgs> AlertMessageEventHandler = (sender, args) =>
                    {
                        if (args.Message.Equals(wasOpenMetaverse.Constants.ALERTS.NO_ROOM_TO_SIT_HERE))
                            succeeded = false;
                        SitEvent.Set();
                    };
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    if (Client.Self.Movement.SitOnGround || !Client.Self.SittingOn.Equals(0))
                        Client.Self.Stand();
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                    // stop non default animations if requested
                    bool deanimate;
                    switch (bool.TryParse(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DEANIMATE)),
                                    corradeCommandParameters.Message)), out deanimate) && deanimate)
                    {
                        case true:
                            // stop all non-built-in animations
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.SignaledAnimations.Copy()
                                .Keys.AsParallel()
                                .Where(o => !wasOpenMetaverse.Helpers.LindenAnimations.Contains(o))
                                .ForAll(o => { Client.Self.AnimationStop(o, true); });
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;
                    }
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.AvatarSitResponse += AvatarSitEventHandler;
                    Client.Self.AlertMessage += AlertMessageEventHandler;
                    Client.Self.RequestSit(primitive.ID, offset);
                    if (!SitEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Self.AvatarSitResponse -= AvatarSitEventHandler;
                        Client.Self.AlertMessage -= AlertMessageEventHandler;
                        Locks.ClientInstanceSelfLock.ExitWriteLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_REQUESTING_SIT);
                    }
                    Client.Self.AvatarSitResponse -= AvatarSitEventHandler;
                    Client.Self.AlertMessage -= AlertMessageEventHandler;
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                    if (!succeeded)
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_SIT);
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.Sit();
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