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
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Command.CorradeCommandParameters, Dictionary<string, string>> give =
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
                    InventoryItem inventoryItem;
                    UUID itemUUID;
                    switch (UUID.TryParse(item, out itemUUID))
                    {
                        case true:
                            inventoryItem =
                                Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode, itemUUID,
                                    corradeConfiguration.ServicesTimeout
                                    ).FirstOrDefault() as InventoryItem;
                            break;
                        default:
                            inventoryItem =
                                Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode, item,
                                    corradeConfiguration.ServicesTimeout)
                                    .FirstOrDefault() as InventoryItem;
                            break;
                    }
                    if (inventoryItem == null)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                    }
                    // Sending an item requires transfer permission.
                    if (!inventoryItem.Permissions.OwnerMask.HasFlag(PermissionMask.Transfer))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_PERMISSIONS_FOR_ITEM);
                    }
                    // Set requested permissions if any on the item.
                    var permissions = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PERMISSIONS)),
                            corradeCommandParameters.Message));
                    if (!string.IsNullOrEmpty(permissions))
                    {
                        if (
                            !Inventory.wasSetInventoryItemPermissions(Client, inventoryItem, permissions,
                                corradeConfiguration.ServicesTimeout))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.SETTING_PERMISSIONS_FAILED);
                        }
                    }
                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Entity>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Enumerations.Entity.AVATAR:
                            UUID agentUUID;
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                            corradeCommandParameters.Message)), out agentUUID) &&
                                !Resolvers.AgentNameToUUID(Client,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(
                                                Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME)),
                                            corradeCommandParameters.Message)),
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                            corradeCommandParameters.Message)),
                                    corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout,
                                    new Time.DecayingAlarm(corradeConfiguration.DataDecayType),
                                    ref agentUUID))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                            }
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.GiveItem(inventoryItem.UUID, inventoryItem.Name,
                                    inventoryItem.AssetType, agentUUID, true);
                            }
                            break;
                        case Enumerations.Entity.OBJECT:
                            float range;
                            if (
                                !float.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                            corradeCommandParameters.Message)),
                                    out range))
                            {
                                range = corradeConfiguration.Range;
                            }
                            Primitive primitive = null;
                            var target = wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                                corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(target))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_TARGET_SPECIFIED);
                            }
                            UUID targetUUID;
                            if (UUID.TryParse(target, out targetUUID))
                            {
                                if (
                                    !Services.FindPrimitive(Client,
                                        targetUUID,
                                        range,
                                        ref primitive,
                                        corradeConfiguration.DataTimeout))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                                }
                            }
                            else
                            {
                                if (
                                    !Services.FindPrimitive(Client,
                                        target,
                                        range,
                                        ref primitive,
                                        corradeConfiguration.DataTimeout))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                                }
                            }
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.UpdateTaskInventory(primitive.LocalID, inventoryItem);
                            }
                            break;
                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }
                };
        }
    }
}