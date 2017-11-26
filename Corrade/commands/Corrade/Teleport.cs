///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Constants;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> teleport =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Movement))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var position = Vector3.Zero;
                    ulong regionHandle = 0;
                    var lookAt = Vector3.Zero;
                    if (!Vector3.TryParse(wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TURNTO)),
                        corradeCommandParameters.Message)), out lookAt))
                        lookAt = Client.Self.LookAt;
                    var landmarkAssetUUID = UUID.Zero;
                    var entity = Reflection.GetEnumValueFromName<Enumerations.Entity>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message)));
                    switch (entity)
                    {
                        case Enumerations.Entity.GLOBAL:
                            if (
                                !Vector3.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                            corradeCommandParameters.Message)),
                                    out position))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_POSITION_PROVIDED);
                            float x = 0;
                            float y = 0;
                            regionHandle = OpenMetaverse.Helpers.GlobalPosToRegionHandle(position.X, position.Y, out x,
                                out y);
                            position.X = x;
                            position.Y = y;
                            break;

                        case Enumerations.Entity.REGION:
                            if (!Vector3.TryParse(wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)), out position))
                                position = Client.Self.SimPosition;
                            // We override the default teleport since region names are unique and case insensitive.
                            var region =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                        corradeCommandParameters.Message));
                            UUID regionUUID;
                            switch (UUID.TryParse(region, out regionUUID))
                            {
                                case true:
                                    if (
                                        !Resolvers.RegionUUIDToHandle(Client, regionUUID,
                                            corradeConfiguration.ServicesTimeout,
                                            ref regionHandle))
                                        throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                                    var parcelUUID = Client.Parcels.RequestRemoteParcelID(new Vector3(128, 128, 0),
                                        regionHandle,
                                        UUID.Zero);
                                    if (parcelUUID.Equals(UUID.Zero))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                                    var parcelInfo = new ParcelInfo();
                                    if (
                                        !Services.GetParcelInfo(Client, parcelUUID,
                                            corradeConfiguration.ServicesTimeout,
                                            ref parcelInfo))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.COULD_NOT_GET_PARCEL_INFO);
                                    region = parcelInfo.SimName;
                                    break;

                                default:
                                    if (string.IsNullOrEmpty(region))
                                    {
                                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                                        region = Client.Network.CurrentSim.Name;
                                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                                    }
                                    break;
                            }
                            // Check if the teleport destination is not too close.
                            if (
                                string.Equals(region, Client.Network.CurrentSim.Name,
                                    StringComparison.OrdinalIgnoreCase) &&
                                Vector3.Distance(Client.Self.SimPosition, position) <
                                wasOpenMetaverse.Constants.REGION.TELEPORT_MINIMUM_DISTANCE)
                                throw new Command.ScriptException(Enumerations.ScriptError.DESTINATION_TOO_CLOSE);
                            if (regionHandle.Equals(0) &&
                                !Resolvers.RegionNameToHandle(Client, region, corradeConfiguration.ServicesTimeout,
                                    ref regionHandle))
                                throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                            break;

                        case Enumerations.Entity.LANDMARK:
                            var item = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                                    corradeCommandParameters.Message));
                            if (!UUID.TryParse(item, out landmarkAssetUUID))
                            {
                                var inventoryItem =
                                    Inventory.FindInventory<InventoryItem>(Client, item,
                                        CORRADE_CONSTANTS.PATH_SEPARATOR,
                                        CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                        corradeConfiguration.ServicesTimeout);
                                if (inventoryItem == null)
                                    throw new Command.ScriptException(Enumerations.ScriptError
                                        .INVENTORY_ITEM_NOT_FOUND);
                                landmarkAssetUUID = inventoryItem.AssetUUID;
                            }
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client) && !TimedTeleportThrottle.IsSafe)
                        throw new Command.ScriptException(Enumerations.ScriptError.TELEPORT_THROTTLED);
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
                    var succeeded = false;
                    switch (entity)
                    {
                        case Enumerations.Entity.GLOBAL:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            succeeded = Client.Self.Teleport(regionHandle, position);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        case Enumerations.Entity.REGION:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            succeeded = Client.Self.Teleport(regionHandle, position, lookAt);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        case Enumerations.Entity.LANDMARK:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            succeeded = Client.Self.Teleport(landmarkAssetUUID);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }
                    if (!succeeded)
                    {
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            Client.Self.TeleportMessage);
                        throw new Command.ScriptException(Enumerations.ScriptError.TELEPORT_FAILED);
                    }

                    bool fly;
                    // perform the post-action
                    switch (bool.TryParse(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FLY)),
                            corradeCommandParameters.Message)), out fly))
                    {
                        case true: // if fly was specified, set the fly state
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.Fly(fly);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;
                    }

                    // Turn to look at the given position.
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.Movement.TurnToward(position, true);
                    Locks.ClientInstanceSelfLock.ExitWriteLock();

                    // Set the camera on the avatar.
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.Movement.Camera.LookAt(
                        Client.Self.SimPosition,
                        Client.Self.SimPosition
                    );
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                    SaveMovementState.Invoke();
                };
        }
    }
}