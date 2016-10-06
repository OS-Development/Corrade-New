///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Constants;
using CorradeConfiguration;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> rez =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var item = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                    }
                    InventoryItem inventoryItem = null;
                    UUID itemUUID;
                    switch (UUID.TryParse(item, out itemUUID))
                    {
                        case true:
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                if (Client.Inventory.Store.Contains(itemUUID))
                                {
                                    inventoryItem = Client.Inventory.Store[itemUUID] as InventoryItem;
                                }
                            }
                            break;
                        default:
                            inventoryItem =
                                Inventory.FindInventory<InventoryItem>(Client, item,
                                    CORRADE_CONSTANTS.PATH_SEPARATOR, corradeConfiguration.ServicesTimeout);
                            break;
                    }
                    if (inventoryItem == null)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                    }
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_POSITION);
                    }
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                        position.Z > wasOpenMetaverse.Constants.PRIMITIVES.MAXIMUM_REZ_HEIGHT)
                    {
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.POSITION_WOULD_EXCEED_MAXIMUM_REZ_ALTITUDE);
                    }
                    Quaternion rotation;
                    if (
                        !Quaternion.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ROTATION)),
                                    corradeCommandParameters.Message)),
                            out rotation))
                    {
                        rotation = Quaternion.Identity;
                    }
                    var region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                corradeCommandParameters.Message));
                    Simulator simulator;
                    lock (Locks.ClientInstanceNetworkLock)
                    {
                        simulator =
                            Client.Network.Simulators.AsParallel().FirstOrDefault(
                                o =>
                                    o.Name.Equals(
                                        string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                        StringComparison.OrdinalIgnoreCase));
                    }
                    if (simulator == null)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                    }
                    Parcel parcel = null;
                    if (
                        !Services.GetParcelAtPosition(Client, simulator, position, corradeConfiguration.ServicesTimeout,
                            ref parcel))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                    }
                    if (!parcel.Flags.IsMaskFlagSet(ParcelFlags.CreateObjects))
                    {
                        if (!simulator.IsEstateManager)
                        {
                            if (!parcel.OwnerID.Equals(Client.Self.AgentID))
                            {
                                if (!parcel.IsGroupOwned && !parcel.GroupID.Equals(corradeCommandParameters.Group.UUID))
                                {
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                                }
                                if (
                                    !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                        corradeCommandParameters.Group.UUID,
                                        GroupPowers.AllowRez,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                                {
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                                }
                            }
                        }
                    }
                    lock (Locks.ClientInstanceInventoryLock)
                    {
                        Client.Inventory.RequestRezFromInventory(simulator, rotation, position, inventoryItem,
                            corradeCommandParameters.Group.UUID);
                    }
                };
        }
    }
}