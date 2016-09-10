///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
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
            public static Action<Command.CorradeCommandParameters, Dictionary<string, string>> renameitem =
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
                    var name = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);
                    }
                    switch (inventoryItem.AssetType)
                    {
                        case AssetType.Folder:
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.MoveFolder(inventoryItem.UUID, inventoryItem.ParentUUID, name);
                            }
                            break;
                        default:
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.MoveItem(inventoryItem.UUID, inventoryItem.ParentUUID, name);
                            }
                            break;
                    }
                };
        }
    }
}