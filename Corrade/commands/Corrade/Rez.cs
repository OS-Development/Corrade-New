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
using wasSharp.Timers;
using Inventory = wasOpenMetaverse.Inventory;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> rez =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var item = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                    InventoryItem inventoryItem = null;
                    UUID itemUUID;
                    switch (UUID.TryParse(item, out itemUUID))
                    {
                        case true:
                            Locks.ClientInstanceInventoryLock.EnterReadLock();
                            if (Client.Inventory.Store.Contains(itemUUID))
                                inventoryItem = Client.Inventory.Store[itemUUID] as InventoryItem;
                            Locks.ClientInstanceInventoryLock.ExitReadLock();
                            break;

                        default:
                            inventoryItem =
                                Inventory.FindInventory<InventoryItem>(Client, item,
                                    CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                    corradeConfiguration.ServicesTimeout);
                            break;
                    }
                    if (inventoryItem == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_POSITION);
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                        position.Z > wasOpenMetaverse.Constants.PRIMITIVES.MAXIMUM_REZ_HEIGHT)
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.POSITION_WOULD_EXCEED_MAXIMUM_REZ_ALTITUDE);
                    Quaternion rotation;
                    if (
                        !Quaternion.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ROTATION)),
                                    corradeCommandParameters.Message)),
                            out rotation))
                        rotation = Quaternion.Identity;
                    var region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                corradeCommandParameters.Message));
                    Locks.ClientInstanceNetworkLock.EnterReadLock();
                    var simulator = Client.Network.Simulators.AsParallel().FirstOrDefault(
                        o =>
                            o.Name.Equals(
                                string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                StringComparison.OrdinalIgnoreCase));
                    Locks.ClientInstanceNetworkLock.ExitReadLock();
                    if (simulator == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                    Parcel parcel = null;
                    if (
                        !Services.GetParcelAtPosition(Client, simulator, position, corradeConfiguration.ServicesTimeout,
                            corradeConfiguration.DataTimeout,
                            ref parcel))
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                    // Check if Corrade has permissions in the parcel group.
                    var initialGroup = Client.Self.ActiveGroup;
                    if (!simulator.IsEstateManager && !parcel.Flags.IsMaskFlagSet(ParcelFlags.CreateObjects) &&
                        !parcel.OwnerID.Equals(Client.Self.AgentID) &&
                        (!parcel.Flags.IsMaskFlagSet(ParcelFlags.CreateGroupObjects) ||
                         !Services.HasGroupPowers(Client, Client.Self.AgentID,
                             parcel.GroupID,
                             GroupPowers.AllowRez,
                             corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                             new DecayingAlarm(corradeConfiguration.DataDecayType))))
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);

                    // Activate parcel group.
                    Locks.ClientInstanceGroupsLock.EnterWriteLock();
                    Client.Groups.ActivateGroup(parcel.GroupID);

                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                    Client.Inventory.RequestRezFromInventory(simulator, rotation, position, inventoryItem,
                        corradeCommandParameters.Group.UUID);
                    Locks.ClientInstanceInventoryLock.ExitWriteLock();

                    // Activate the initial group.
                    Client.Groups.ActivateGroup(initialGroup);
                    Locks.ClientInstanceGroupsLock.ExitWriteLock();
                };
        }
    }
}