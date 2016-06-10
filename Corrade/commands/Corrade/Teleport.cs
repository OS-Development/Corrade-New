///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Helpers = wasOpenMetaverse.Helpers;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> teleport =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Movement))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                    {
                        position = Client.Self.SimPosition;
                    }
                    Vector3 lookAt;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TURNTO)),
                                    corradeCommandParameters.Message)),
                            out lookAt))
                    {
                        lookAt = Client.Self.LookAt;
                    }
                    // We override the default teleport since region names are unique and case insensitive.
                    var region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.REGION)),
                                corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(region))
                    {
                        region = Client.Network.CurrentSim.Name;
                    }
                    // Check if the teleport destination is not too close.
                    if (region.Equals(Client.Network.CurrentSim.Name) &&
                        Vector3.Distance(Client.Self.SimPosition, position) <
                        Constants.REGION.TELEPORT_MINIMUM_DISTANCE)
                    {
                        throw new ScriptException(ScriptError.DESTINATION_TOO_CLOSE);
                    }
                    ulong regionHandle = 0;
                    if (
                        !Resolvers.RegionNameToHandle(Client, region, corradeConfiguration.ServicesTimeout,
                            ref regionHandle))
                    {
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    }
                    var TeleportEvent = new ManualResetEvent(false);
                    var succeeded = false;
                    EventHandler<TeleportEventArgs> TeleportEventHandler = (sender, args) =>
                    {
                        switch (args.Status)
                        {
                            case TeleportStatus.Cancelled:
                            case TeleportStatus.Failed:
                            case TeleportStatus.Finished:
                                succeeded = args.Status.Equals(TeleportStatus.Finished);
                                TeleportEvent.Set();
                                break;
                        }
                    };
                    if (Helpers.IsSecondLife(Client) && !TimedTeleportThrottle.IsSafe)
                    {
                        throw new ScriptException(ScriptError.TELEPORT_THROTTLED);
                    }
                    lock (Locks.ClientInstanceSelfLock)
                    {
                        if (Client.Self.Movement.SitOnGround || !Client.Self.SittingOn.Equals(0))
                        {
                            Client.Self.Stand();
                        }
                    }
                    // stop non default animations if requested
                    bool deanimate;
                    switch (bool.TryParse(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DEANIMATE)),
                            corradeCommandParameters.Message)), out deanimate) && deanimate)
                    {
                        case true:
                            // stop all non-built-in animations
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.SignaledAnimations.Copy()
                                    .Keys.AsParallel()
                                    .Where(o => !Helpers.LindenAnimations.Contains(o))
                                    .ForAll(o => { Client.Self.AnimationStop(o, true); });
                            }
                            break;
                    }
                    lock (Locks.ClientInstanceSelfLock)
                    {
                        Client.Self.TeleportProgress += TeleportEventHandler;
                        Client.Self.Teleport(regionHandle, position, lookAt);
                        if (!TeleportEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Self.TeleportProgress -= TeleportEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_DURING_TELEPORT);
                        }
                        Client.Self.TeleportProgress -= TeleportEventHandler;
                    }
                    if (!succeeded)
                    {
                        throw new ScriptException(ScriptError.TELEPORT_FAILED);
                    }
                    // If the teleport succeeded, cache the region handle.
                    Cache.RegionCache.Add(new Cache.Regions
                    {
                        Name = Client.Network.CurrentSim.Name,
                        Handle = Client.Network.CurrentSim.Handle
                    });
                    bool fly;
                    // perform the post-action
                    switch (bool.TryParse(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FLY)),
                            corradeCommandParameters.Message)), out fly))
                    {
                        case true: // if fly was specified, set the fly state
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.Fly(fly);
                            }
                            break;
                    }
                    // Set the camera on the avatar.
                    lock (Locks.ClientInstanceSelfLock)
                    {
                        Client.Self.Movement.Camera.LookAt(
                            Client.Self.SimPosition,
                            Client.Self.SimPosition
                            );
                    }
                    SaveMovementState.Invoke();
                };
        }
    }
}