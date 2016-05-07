///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> updateprimitiveinventory =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
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
                    string item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                    {
                        throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
                    }
                    UUID itemUUID;
                    if (UUID.TryParse(item, out itemUUID))
                    {
                        if (
                            !Services.FindPrimitive(Client,
                                itemUUID,
                                range,
                                corradeConfiguration.Range,
                                ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                        {
                            throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                        }
                    }
                    else
                    {
                        if (
                            !Services.FindPrimitive(Client,
                                item,
                                range,
                                corradeConfiguration.Range,
                                ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                        {
                            throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                        }
                    }
                    string entity =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message));
                    UUID entityUUID;
                    if (!UUID.TryParse(entity, out entityUUID))
                    {
                        if (string.IsNullOrEmpty(entity))
                        {
                            throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                        }
                        entityUUID = UUID.Zero;
                    }
                    Simulator simulator;
                    lock (Locks.ClientInstanceNetworkLock)
                    {
                        simulator = Client.Network.Simulators.AsParallel()
                            .FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle));
                    }
                    if (simulator == null)
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    InventoryBase inventoryBaseItem;
                    switch (
                        Reflection.GetEnumValueFromName<Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Action.ADD:
                            inventoryBaseItem =
                                Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                    !entityUUID.Equals(UUID.Zero) ? entityUUID.ToString() : entity
                                    ).FirstOrDefault();
                            if (inventoryBaseItem == null)
                            {
                                throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                            }
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.UpdateTaskInventory(primitive.LocalID,
                                    inventoryBaseItem as InventoryItem);
                            }
                            break;
                        case Action.REMOVE:
                            if (entityUUID.Equals(UUID.Zero))
                            {
                                inventoryBaseItem = Client.Inventory.GetTaskInventory(primitive.ID,
                                    primitive.LocalID,
                                    (int) corradeConfiguration.ServicesTimeout)
                                    .AsParallel()
                                    .FirstOrDefault(o => o.Name.Equals(entity));
                                if (inventoryBaseItem == null)
                                {
                                    throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                                }
                                entityUUID = inventoryBaseItem.UUID;
                            }
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.RemoveTaskInventory(primitive.LocalID, entityUUID, simulator);
                            }
                            break;
                        case Action.TAKE:
                            inventoryBaseItem = !entityUUID.Equals(UUID.Zero)
                                ? Client.Inventory.GetTaskInventory(primitive.ID, primitive.LocalID,
                                    (int) corradeConfiguration.ServicesTimeout)
                                    .AsParallel()
                                    .FirstOrDefault(o => o.UUID.Equals(entityUUID))
                                : Client.Inventory.GetTaskInventory(primitive.ID, primitive.LocalID,
                                    (int) corradeConfiguration.ServicesTimeout)
                                    .AsParallel()
                                    .FirstOrDefault(o => o.Name.Equals(entity));
                            InventoryItem inventoryItem = inventoryBaseItem as InventoryItem;
                            if (inventoryItem == null)
                            {
                                throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                            }
                            UUID folderUUID;
                            string folder =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FOLDER)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(folder) || !UUID.TryParse(folder, out folderUUID))
                            {
                                folderUUID =
                                    Client.Inventory.Store.Items[
                                        Client.Inventory.FindFolderForType(inventoryItem.AssetType)].Data
                                        .UUID;
                            }
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.MoveTaskInventory(primitive.LocalID, inventoryItem.UUID, folderUUID,
                                    simulator);
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}