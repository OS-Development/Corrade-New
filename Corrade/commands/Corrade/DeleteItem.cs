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
            public static Action<Command.CorradeCommandParameters, Dictionary<string, string>> deleteitem =
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
                    var items =
                        new HashSet<InventoryItem>();
                    UUID itemUUID;
                    switch (UUID.TryParse(item, out itemUUID))
                    {
                        case true:
                            items.UnionWith(Inventory
                                .FindInventory<InventoryBase>(Client,
                                    Client.Inventory.Store.RootNode,
                                    itemUUID, corradeConfiguration.ServicesTimeout)
                                .ToArray()
                                .AsParallel()
                                .OfType<InventoryItem>());
                            break;
                        default:
                            items.UnionWith(
                                Inventory
                                    .FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode, item,
                                        corradeConfiguration.ServicesTimeout)
                                    .ToArray()
                                    .AsParallel()
                                    .OfType<InventoryItem>());
                            break;
                    }
                    if (!items.Any())
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                    }
                    items.AsParallel().ForAll(o =>
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