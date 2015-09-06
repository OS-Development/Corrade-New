using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> give = (commandGroup, message, result) =>
            {
                if (
                    !HasCorradePermission(commandGroup.Name,
                        (int) Permissions.Inventory))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                object item =
                    StringOrUUID(
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)),
                            message)));
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
                        break;
                    default:
                        throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
                }
                switch (
                    wasGetEnumValueFromDescription<Entity>(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ENTITY)),
                                message)).ToLowerInvariant()))
                {
                    case Entity.AVATAR:
                        UUID agentUUID;
                        if (
                            !UUID.TryParse(
                                wasInput(
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT)),
                                        message)), out agentUUID) && !AgentNameToUUID(
                                            wasInput(
                                                wasKeyValueGet(
                                                    wasOutput(
                                                        wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME)),
                                                    message)),
                                            wasInput(
                                                wasKeyValueGet(
                                                    wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME)),
                                                    message)),
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
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.RANGE)),
                                        message)),
                                out range))
                        {
                            range = corradeConfiguration.Range;
                        }
                        Primitive primitive = null;
                        if (
                            !FindPrimitive(
                                StringOrUUID(wasInput(
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TARGET)),
                                        message))),
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