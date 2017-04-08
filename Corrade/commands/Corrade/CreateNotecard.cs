///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using CorradeConfigurationSharp;
using OpenMetaverse;
using OpenMetaverse.Assets;
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Threading;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

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
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);
                    }
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
                    if (!CreateNotecardEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, false))
                    {
                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_CREATING_ITEM);
                    }
                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                    if (!succeeded)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_CREATE_ITEM);
                    }
                    var blank = new AssetNotecard
                    {
                        BodyText = wasOpenMetaverse.Constants.ASSETS.NOTECARD.NEWLINE
                    };
                    blank.Encode();
                    var UploadBlankNotecardEvent = new ManualResetEvent(false);
                    succeeded = false;
                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                    Client.Inventory.RequestUploadNotecardAsset(blank.AssetData, newItem.UUID,
                            delegate (bool completed, string status, UUID itemUUID, UUID assetUUID)
                            {
                                succeeded = completed;
                                UploadBlankNotecardEvent.Set();
                            });
                    if (!UploadBlankNotecardEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, false))
                    {
                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ITEM);
                    }
                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                    if (!succeeded)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_UPLOAD_ITEM);
                    }
                    var inventoryItemUUID = UUID.Zero;
                    var inventoryAssetUUID = UUID.Zero;
                    if (!string.IsNullOrEmpty(text))
                    {
                        var notecard = new AssetNotecard
                        {
                            BodyText = text
                        };
                        notecard.Encode();
                        var UploadNotecardDataEvent = new ManualResetEvent(false);
                        succeeded = false;
                        Locks.ClientInstanceInventoryLock.EnterWriteLock();
                        Client.Inventory.RequestUploadNotecardAsset(notecard.AssetData, newItem.UUID,
                                delegate (bool completed, string status, UUID itemUUID, UUID assetUUID)
                                {
                                    succeeded = completed;
                                    inventoryItemUUID = itemUUID;
                                    inventoryAssetUUID = assetUUID;
                                    UploadNotecardDataEvent.Set();
                                });
                        if (!UploadNotecardDataEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, false))
                        {
                            Locks.ClientInstanceInventoryLock.ExitWriteLock();
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ITEM_DATA);
                        }
                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                        if (!succeeded)
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_UPLOAD_ITEM_DATA);
                        }
                    }

                    // Return the item and asset UUID.
                    result.Add(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                        CSV.FromEnumerable(new[]
                        {
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                            inventoryItemUUID.ToString(),
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ASSET)),
                            inventoryAssetUUID.ToString()
                        }));
                };
        }
    }
}
