///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using CorradeConfigurationSharp;
using OpenMetaverse;
using OpenMetaverse.Assets;
using System;
using System.Linq;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;
using Inventory = wasOpenMetaverse.Inventory;
using Corrade.Constants;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> createnotecard =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int)Configuration.Permissions.Inventory))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string text;
                    switch (Reflection.GetEnumValueFromName<Enumerations.Entity>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message))
                        ))
                    {
                        case Enumerations.Entity.FILE:
                            if (
                                !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                    (int)Configuration.Permissions.System))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                            }
                            var path =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PATH)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(path))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_PATH_PROVIDED);
                            }
                            // Read from file.
                            try
                            {
                                using (
                                    var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read,
                                        16384, true))
                                {
                                    using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                                    {
                                        text = streamReader.ReadToEnd();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), ex.Message);
                                throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_READ_FILE);
                            }
                            break;

                        case Enumerations.Entity.TEXT:
                            text =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TEXT)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(text))
                                text = wasOpenMetaverse.Constants.ASSETS.NOTECARD.NEWLINE;
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                        Encoding.UTF8.GetByteCount(text) >
                        wasOpenMetaverse.Constants.ASSETS.NOTECARD.MAXIMUM_BODY_LENTH)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NOTECARD_MESSAGE_BODY_TOO_LARGE);
                    }

                    var name =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);

                    var attachments = new List<InventoryItem>(CSV.ToEnumerable(wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ATTACHMENTS)),
                            corradeCommandParameters.Message)))
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o))
                        .Select(
                            o =>
                            {
                                InventoryItem inventoryItem = null;
                                UUID itemUUID;
                                switch (UUID.TryParse(o, out itemUUID))
                                {
                                    case true:
                                        Locks.ClientInstanceInventoryLock.EnterReadLock();
                                        if (Client.Inventory.Store.Contains(itemUUID))
                                        {
                                            inventoryItem = Client.Inventory.Store[itemUUID] as InventoryItem;
                                        }
                                        Locks.ClientInstanceInventoryLock.ExitReadLock();
                                        break;

                                    default:
                                        inventoryItem =
                                            Inventory.FindInventory<InventoryItem>(Client, o,
                                                CORRADE_CONSTANTS.PATH_SEPARATOR,
                                                CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                                corradeConfiguration.ServicesTimeout);
                                        break;
                                }

                                return inventoryItem;
                            }).Where(o => o != null));

                    // Create notecard.
                    var CreateNotecardEvent = new ManualResetEvent(false);
                    var succeeded = false;
                    InventoryItem newItem = null;
                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                    Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(AssetType.Notecard),
                            name,
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                    corradeCommandParameters.Message)),
                            AssetType.Notecard,
                            UUID.Random(), InventoryType.Notecard, PermissionMask.All,
                            delegate (bool completed, InventoryItem createdItem)
                            {
                                succeeded = completed;
                                newItem = createdItem;
                                CreateNotecardEvent.Set();
                            });
                    if (!CreateNotecardEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, true))
                    {
                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_CREATING_ITEM);
                    }
                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                    if (!succeeded)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_CREATE_ITEM);
                    }

                    // Upload blank notecard.
                    AssetNotecard emptyNotecard = new AssetNotecard
                    {
                        BodyText = wasOpenMetaverse.Constants.ASSETS.NOTECARD.NEWLINE
                    };
                    emptyNotecard.Encode();

                    var CreateBlankNotecardEvent = new ManualResetEvent(false);
                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                    Client.Inventory.RequestUploadNotecardAsset(emptyNotecard.AssetData, newItem.UUID,
                        delegate (bool completed, string status, UUID itemUUID, UUID assetUUID)
                        {
                            succeeded = completed;
                            newItem.UUID = itemUUID;
                            newItem.AssetUUID = assetUUID;
                            CreateBlankNotecardEvent.Set();
                        });
                    if (!CreateBlankNotecardEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, true))
                    {
                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_CREATING_ITEM);
                    }
                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                    if (!succeeded)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_CREATE_ITEM);
                    }

                    // Upload the real content.
                    var notecard = new AssetNotecard(newItem.AssetUUID, null);
                    notecard.BodyText = text;
                    notecard.EmbeddedItems = new List<InventoryItem>();
                    // Add the attachments.
                    for (var i = 0; i < attachments.Count; ++i)
                    {
                        notecard.EmbeddedItems.Add(attachments[i]);
                        notecard.BodyText += (char)0xdbc0;
                        notecard.BodyText += (char)(0xdc00 + i);
                    }
                    notecard.Encode();

                    var UploadNotecardDataEvent = new ManualResetEvent(false);
                    succeeded = false;
                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                    Client.Inventory.RequestUploadNotecardAsset(notecard.AssetData, newItem.UUID,
                            delegate (bool completed, string status, UUID itemUUID, UUID assetUUID)
                            {
                                succeeded = completed;
                                newItem.UUID = itemUUID;
                                newItem.AssetUUID = assetUUID;
                                UploadNotecardDataEvent.Set();
                            });
                    if (!UploadNotecardDataEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, true))
                    {
                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ITEM_DATA);
                    }
                    Locks.ClientInstanceInventoryLock.ExitWriteLock();

                    if (!succeeded)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_UPLOAD_ITEM_DATA);
                    }

                    // Return the item and asset UUID.
                    result.Add(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                        CSV.FromEnumerable(new[]
                        {
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                            newItem.UUID.ToString(),
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ASSET)),
                            newItem.AssetUUID.ToString()
                        }));
                };
        }
    }
}
