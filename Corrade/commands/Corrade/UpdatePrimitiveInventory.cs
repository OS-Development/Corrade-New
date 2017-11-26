///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                updateprimitiveinventory
                    =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Interact))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
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
                        var entity =
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                    corradeCommandParameters.Message));
                        UUID entityUUID;
                        if (!UUID.TryParse(entity, out entityUUID))
                        {
                            if (string.IsNullOrEmpty(entity))
                                throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                            entityUUID = UUID.Zero;
                        }
                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                        var simulator = Client.Network.Simulators.AsParallel()
                            .FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle));
                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                        if (simulator == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                        InventoryBase inventoryBaseItem = null;
                        switch (
                            Reflection.GetEnumValueFromName<Enumerations.Action>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                        corradeCommandParameters.Message))))
                        {
                            case Enumerations.Action.ADD:
                                switch (entityUUID.Equals(UUID.Zero))
                                {
                                    case true:
                                        inventoryBaseItem =
                                            Inventory.FindInventory<InventoryBase>(Client, entity,
                                                CORRADE_CONSTANTS.PATH_SEPARATOR,
                                                CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                                corradeConfiguration.ServicesTimeout);
                                        break;

                                    default:
                                        Locks.ClientInstanceInventoryLock.EnterReadLock();
                                        if (Client.Inventory.Store.Contains(entityUUID))
                                            inventoryBaseItem = Client.Inventory.Store[itemUUID];
                                        Locks.ClientInstanceInventoryLock.ExitReadLock();
                                        break;
                                }
                                if (inventoryBaseItem == null)
                                    throw new Command.ScriptException(Enumerations.ScriptError
                                        .INVENTORY_ITEM_NOT_FOUND);
                                Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                Client.Inventory.UpdateTaskInventory(primitive.LocalID,
                                    inventoryBaseItem as InventoryItem);
                                Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                break;

                            case Enumerations.Action.REMOVE:
                                if (entityUUID.Equals(UUID.Zero))
                                {
                                    inventoryBaseItem = Client.Inventory.GetTaskInventory(primitive.ID,
                                            primitive.LocalID,
                                            (int) corradeConfiguration.ServicesTimeout)
                                        .AsParallel()
                                        .FirstOrDefault(o => o.Name.Equals(entity));
                                    if (inventoryBaseItem == null)
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                                    entityUUID = inventoryBaseItem.UUID;
                                }
                                Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                Client.Inventory.RemoveTaskInventory(primitive.LocalID, entityUUID, simulator);
                                Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                break;

                            case Enumerations.Action.TAKE:
                                inventoryBaseItem = !entityUUID.Equals(UUID.Zero)
                                    ? Client.Inventory.GetTaskInventory(primitive.ID, primitive.LocalID,
                                            (int) corradeConfiguration.ServicesTimeout)
                                        .AsParallel()
                                        .FirstOrDefault(o => o.UUID.Equals(entityUUID))
                                    : Client.Inventory.GetTaskInventory(primitive.ID, primitive.LocalID,
                                            (int) corradeConfiguration.ServicesTimeout)
                                        .AsParallel()
                                        .FirstOrDefault(o => o.Name.Equals(entity));
                                var inventoryItem = inventoryBaseItem as InventoryItem;
                                if (inventoryItem == null)
                                    throw new Command.ScriptException(Enumerations.ScriptError
                                        .INVENTORY_ITEM_NOT_FOUND);
                                UUID folderUUID;
                                var folder =
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FOLDER)),
                                            corradeCommandParameters.Message));
                                if (string.IsNullOrEmpty(folder) || !UUID.TryParse(folder, out folderUUID))
                                    folderUUID =
                                        Client.Inventory.Store.Items[
                                                Client.Inventory.FindFolderForType(inventoryItem.AssetType)].Data
                                            .UUID;
                                Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                Client.Inventory.MoveTaskInventory(primitive.LocalID, inventoryItem.UUID, folderUUID,
                                    simulator);
                                Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                break;

                            default:
                                throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                        }
                    };
        }
    }
}