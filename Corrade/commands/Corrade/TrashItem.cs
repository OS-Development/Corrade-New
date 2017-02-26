///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using String = wasSharp.String;
using System.Collections.Generic;
using Corrade.Constants;
using CorradeConfigurationSharp;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> trashitem =
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
                    InventoryBase inventoryBase = null;
                    UUID itemUUID;
                    switch (UUID.TryParse(item, out itemUUID))
                    {
                        case true:
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                if (Client.Inventory.Store.Contains(itemUUID))
                                {
                                    inventoryBase = Client.Inventory.Store[itemUUID];
                                }
                            }
                            break;
                        default:
                            inventoryBase = Inventory.FindInventory<InventoryBase>(Client, item,
                                CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                corradeConfiguration.ServicesTimeout);
                            break;
                    }
                    if (inventoryBase == null)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                    }
                    // Get the parent UUID.
                    var parentUUID = UUID.Zero;
                    switch (inventoryBase.ParentUUID.Equals(UUID.Zero))
                    {
                        case true:
                            UUID rootFolderUUID;
                            UUID libraryFolderUUID;
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                rootFolderUUID = Client.Inventory.Store.RootFolder.UUID;
                                libraryFolderUUID = Client.Inventory.Store.LibraryFolder.UUID;
                            }
                            if (inventoryBase.UUID.Equals(rootFolderUUID))
                            {
                                parentUUID = rootFolderUUID;
                                break;
                            }
                            if (inventoryBase.UUID.Equals(libraryFolderUUID))
                            {
                                parentUUID = libraryFolderUUID;
                            }
                            break;
                        default:
                            parentUUID = inventoryBase.ParentUUID;
                            break;
                    }
                    // Move the item or folder.
                    switch (inventoryBase is InventoryFolder)
                    {
                        case true:
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.MoveFolder(inventoryBase.UUID,
                                    Client.Inventory.FindFolderForType(AssetType.TrashFolder));
                            }
                            break;
                        default:
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.MoveItem(inventoryBase.UUID,
                                    Client.Inventory.FindFolderForType(AssetType.TrashFolder));
                            }
                            break;
                    }
                    // Mark the parent as needing an update.
                    lock (Locks.ClientInstanceInventoryLock)
                    {
                        Client.Inventory.Store.GetNodeFor(parentUUID).NeedsUpdate = true;
                        Client.Inventory.Store.GetNodeFor(Client.Inventory.FindFolderForType(AssetType.TrashFolder))
                            .NeedsUpdate = true;
                    }
                };
        }
    }
}