///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                copynotecardasset =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Inventory))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        // Get the optional name to rename to.
                        var name = wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                                corradeCommandParameters.Message));
                        // Get the target asset.
                        var asset = wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ASSET)),
                                corradeCommandParameters.Message));
                        UUID assetUUID;
                        if (string.IsNullOrEmpty(asset) || !UUID.TryParse(asset, out assetUUID))
                            throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET);
                        // Get the target notecard.
                        var notecard = wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                                corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(notecard))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                        InventoryNotecard inventoryNotecard = null;
                        UUID notecardUUID;
                        switch (UUID.TryParse(notecard, out notecardUUID))
                        {
                            case true:
                                Locks.ClientInstanceInventoryLock.EnterReadLock();
                                if (Client.Inventory.Store.Contains(notecardUUID))
                                    inventoryNotecard = Client.Inventory.Store[notecardUUID] as InventoryNotecard;
                                Locks.ClientInstanceInventoryLock.ExitReadLock();
                                break;

                            default:
                                inventoryNotecard =
                                    Inventory.FindInventory<InventoryNotecard>(Client, notecard,
                                        CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                        corradeConfiguration.ServicesTimeout);
                                break;
                        }
                        if (inventoryNotecard == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                        // Get the target folder.
                        var folder = wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FOLDER)),
                                corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(folder))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_FOLDER_SPECIFIED);
                        InventoryFolder inventoryFolder = null;
                        UUID folderUUID;
                        switch (UUID.TryParse(folder, out folderUUID))
                        {
                            case true:
                                Locks.ClientInstanceInventoryLock.EnterReadLock();
                                if (Client.Inventory.Store.Contains(folderUUID))
                                    inventoryFolder = Client.Inventory.Store[folderUUID] as InventoryFolder;
                                Locks.ClientInstanceInventoryLock.ExitReadLock();
                                break;

                            default:
                                inventoryFolder =
                                    Inventory.FindInventory<InventoryFolder>(Client, folder,
                                        CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                        corradeConfiguration.ServicesTimeout);
                                break;
                        }
                        if (inventoryFolder == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.FOLDER_NOT_FOUND);

                        var itemUUID = UUID.Zero;
                        Locks.ClientInstanceInventoryLock.EnterWriteLock();
                        Client.Inventory.RequestCopyItemFromNotecard(UUID.Zero, notecardUUID, folderUUID, assetUUID,
                            o =>
                            {
                                // If a name was passed, then rename the item.
                                if (o is InventoryItem && !string.IsNullOrEmpty(name))
                                {
                                    o.Name = name;
                                    Client.Inventory.RequestUpdateItem(o as InventoryItem);
                                }
                                itemUUID = o.UUID;
                            });
                        Locks.ClientInstanceInventoryLock.ExitWriteLock();

                        if (!itemUUID.Equals(UUID.Zero))
                            result.Add(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                itemUUID.ToString());
                    };
        }
    }
}