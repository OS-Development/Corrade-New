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
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> animation =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
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
                    switch (
                        Reflection.GetEnumValueFromName<Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Action.START:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.AnimationStart(inventoryItem.AssetUUID, true);
                            }
                            break;
                        case Action.STOP:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.AnimationStop(inventoryItem.AssetUUID, true);
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ANIMATION_ACTION);
                    }
                };
        }
    }
}