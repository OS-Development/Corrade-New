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
            public static Action<Group, string, Dictionary<string, string>> updateprimitiveinventory =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.RANGE)), message)),
                            out range))
                    {
                        range = corradeConfiguration.Range;
                    }
                    Primitive primitive = null;
                    if (
                        !FindPrimitive(
                            StringOrUUID(wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)), message))),
                            range,
                            ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                    {
                        throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                    }
                    string entity =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ENTITY)),
                            message));
                    UUID entityUUID;
                    if (!UUID.TryParse(entity, out entityUUID))
                    {
                        if (string.IsNullOrEmpty(entity))
                        {
                            throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                        }
                        entityUUID = UUID.Zero;
                    }
                    InventoryBase inventoryBaseItem;
                    switch (
                        wasGetEnumValueFromDescription<Action>(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)),
                                    message)).ToLowerInvariant()))
                    {
                        case Action.ADD:
                            inventoryBaseItem =
                                FindInventory<InventoryBase>(Client.Inventory.Store.RootNode,
                                    !entityUUID.Equals(UUID.Zero) ? entityUUID.ToString() : entity
                                    ).FirstOrDefault();
                            if (inventoryBaseItem == null)
                            {
                                throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                            }
                            Client.Inventory.UpdateTaskInventory(primitive.LocalID,
                                inventoryBaseItem as InventoryItem);
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
                            Client.Inventory.RemoveTaskInventory(primitive.LocalID, entityUUID,
                                Client.Network.Simulators.FirstOrDefault(
                                    o => o.Handle.Equals(primitive.RegionHandle)));
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
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.FOLDER)),
                                        message));
                            if (string.IsNullOrEmpty(folder) || !UUID.TryParse(folder, out folderUUID))
                            {
                                folderUUID =
                                    Client.Inventory.Store.Items[
                                        Client.Inventory.FindFolderForType(inventoryItem.AssetType)].Data
                                        .UUID;
                            }
                            Client.Inventory.MoveTaskInventory(primitive.LocalID, inventoryItem.UUID, folderUUID,
                                Client.Network.Simulators.FirstOrDefault(
                                    o => o.Handle.Equals(primitive.RegionHandle)));
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}