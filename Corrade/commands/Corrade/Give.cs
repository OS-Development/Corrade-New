///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> give =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string item = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                    {
                        throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
                    }
                    InventoryItem inventoryItem;
                    UUID itemUUID;
                    if (UUID.TryParse(item, out itemUUID))
                    {
                        InventoryBase inventoryBaseItem =
                            Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode, itemUUID
                                ).FirstOrDefault();
                        if (inventoryBaseItem == null)
                        {
                            throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                        }
                        inventoryItem = inventoryBaseItem as InventoryItem;
                    }
                    else
                    {
                        // attempt regex and then fall back to string
                        InventoryBase inventoryBaseItem = null;
                        try
                        {
                            inventoryBaseItem =
                                Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                    new Regex(item, RegexOptions.Compiled | RegexOptions.IgnoreCase)).FirstOrDefault();
                        }
                        catch (Exception)
                        {
                            // not a regex so we do not care
                            inventoryBaseItem =
                                Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode, item)
                                    .FirstOrDefault();
                        }
                        if (inventoryBaseItem == null)
                        {
                            throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                        }
                        inventoryItem = inventoryBaseItem as InventoryItem;
                    }
                    if (inventoryItem == null)
                    {
                        throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                    }
                    // Sending an item requires transfer permission.
                    if (!inventoryItem.Permissions.OwnerMask.HasFlag(PermissionMask.Transfer))
                    {
                        throw new ScriptException(ScriptError.NO_PERMISSIONS_FOR_ITEM);
                    }
                    // Set requested permissions if any on the item.
                    string permissions = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.PERMISSIONS)),
                            corradeCommandParameters.Message));
                    if (!string.IsNullOrEmpty(permissions))
                    {
                        if (
                            !Inventory.wasSetInventoryItemPermissions(Client, inventoryItem, permissions,
                                corradeConfiguration.ServicesTimeout))
                        {
                            throw new ScriptException(ScriptError.SETTING_PERMISSIONS_FAILED);
                        }
                    }
                    switch (
                        Reflection.GetEnumValueFromName<Entity>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Entity.AVATAR:
                            UUID agentUUID;
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT)),
                                            corradeCommandParameters.Message)), out agentUUID) &&
                                !Resolvers.AgentNameToUUID(Client,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(
                                                Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME)),
                                            corradeCommandParameters.Message)),
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME)),
                                            corradeCommandParameters.Message)),
                                    corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout,
                                    new Time.DecayingAlarm(corradeConfiguration.DataDecayType),
                                    ref agentUUID))
                            {
                                throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                            }
                            Client.Inventory.GiveItem(inventoryItem.UUID, inventoryItem.Name,
                                inventoryItem.AssetType, agentUUID, true);
                            break;
                        case Entity.OBJECT:
                            float range;
                            if (
                                !float.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.RANGE)),
                                            corradeCommandParameters.Message)),
                                    out range))
                            {
                                range = corradeConfiguration.Range;
                            }
                            Primitive primitive = null;
                            string target = wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TARGET)),
                                corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(target))
                            {
                                throw new ScriptException(ScriptError.NO_TARGET_SPECIFIED);
                            }
                            UUID targetUUID;
                            if (UUID.TryParse(target, out targetUUID))
                            {
                                if (
                                    !Services.FindPrimitive(Client,
                                        targetUUID,
                                        range,
                                        corradeConfiguration.Range,
                                        ref primitive, corradeConfiguration.ServicesTimeout,
                                        corradeConfiguration.DataTimeout,
                                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                                {
                                    throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                                }
                            }
                            else
                            {
                                if (
                                    !Services.FindPrimitive(Client,
                                        target,
                                        range,
                                        corradeConfiguration.Range,
                                        ref primitive, corradeConfiguration.ServicesTimeout,
                                        corradeConfiguration.DataTimeout,
                                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                                {
                                    throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                                }
                            }
                            Client.Inventory.UpdateTaskInventory(primitive.LocalID, inventoryItem);
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                    }
                };
        }
    }
}