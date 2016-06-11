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
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getinventorydata =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var item = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                    {
                        throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
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
                        throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                    }
                    var data = GetStructuredData(inventoryItem,
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                            corradeCommandParameters.Message))).ToList();
                    if (data.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                    }
                };
        }
    }
}