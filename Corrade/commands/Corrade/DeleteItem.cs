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
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> deleteitem =
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
                    HashSet<InventoryItem> items =
                        new HashSet<InventoryItem>();
                    UUID itemUUID;
                    if (UUID.TryParse(item, out itemUUID))
                    {
                        items.UnionWith(Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                            itemUUID).Select(o => o as InventoryItem));
                    }
                    else
                    {
                        // attempt regex and then fall back to string
                        try
                        {
                            items.UnionWith(
                                Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                    new Regex(item, RegexOptions.Compiled | RegexOptions.IgnoreCase))
                                    .Select(o => o as InventoryItem));
                        }
                        catch (Exception)
                        {
                            // not a regex so we do not care
                            items.UnionWith(
                                Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode, item)
                                    .Select(o => o as InventoryItem));
                        }
                    }
                    if (!items.Any())
                    {
                        throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                    }
                    Parallel.ForEach(items, o =>
                    {
                        switch (o.AssetType)
                        {
                            case AssetType.Folder:
                                lock (Locks.ClientInstanceInventoryLock)
                                {
                                    Client.Inventory.MoveFolder(o.UUID,
                                        Client.Inventory.FindFolderForType(AssetType.TrashFolder));
                                }
                                break;
                            default:
                                lock (Locks.ClientInstanceInventoryLock)
                                {
                                    Client.Inventory.MoveItem(o.UUID,
                                        Client.Inventory.FindFolderForType(AssetType.TrashFolder));
                                }
                                break;
                        }
                    });
                };
        }
    }
}