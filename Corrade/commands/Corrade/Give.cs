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
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> give =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Inventory))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    object item =
                        StringOrUUID(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                                    corradeCommandParameters.Message)));
                    InventoryItem inventoryItem;
                    switch (item != null)
                    {
                        case true:
                            InventoryBase inventoryBaseItem =
                                FindInventory<InventoryBase>(Client.Inventory.Store.RootNode, item
                                    ).FirstOrDefault();
                            if (inventoryBaseItem == null)
                            {
                                throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                            }
                            inventoryItem = inventoryBaseItem as InventoryItem;
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
                                if (!wasSetInventoryItemPermissions(inventoryItem, permissions))
                                {
                                    throw new ScriptException(ScriptError.SETTING_PERMISSIONS_FAILED);
                                }
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
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
                                            corradeCommandParameters.Message)), out agentUUID) && !AgentNameToUUID(
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
                            if (
                                !FindPrimitive(
                                    StringOrUUID(wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TARGET)),
                                            corradeCommandParameters.Message))),
                                    range,
                                    ref primitive, corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout))
                            {
                                throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
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