///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using OpenMetaverse.Assets;
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
                            (int) Configuration.Permissions.Inventory))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var text =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TEXT)),
                            corradeCommandParameters.Message));
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
                    lock (Locks.ClientInstanceInventoryLock)
                    {
                        Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(AssetType.Notecard),
                            name,
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                    corradeCommandParameters.Message)),
                            AssetType.Notecard,
                            UUID.Random(), InventoryType.Notecard, PermissionMask.All,
                            delegate(bool completed, InventoryItem createdItem)
                            {
                                succeeded = completed;
                                newItem = createdItem;
                                CreateNotecardEvent.Set();
                            });
                        if (!CreateNotecardEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_CREATING_ITEM);
                        }
                    }
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
                    lock (Locks.ClientInstanceInventoryLock)
                    {
                        Client.Inventory.RequestUploadNotecardAsset(blank.AssetData, newItem.UUID,
                            delegate(bool completed, string status, UUID itemUUID, UUID assetUUID)
                            {
                                succeeded = completed;
                                UploadBlankNotecardEvent.Set();
                            });
                        if (!UploadBlankNotecardEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ITEM);
                        }
                    }
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
                        lock (Locks.ClientInstanceInventoryLock)
                        {
                            Client.Inventory.RequestUploadNotecardAsset(notecard.AssetData, newItem.UUID,
                                delegate(bool completed, string status, UUID itemUUID, UUID assetUUID)
                                {
                                    succeeded = completed;
                                    inventoryItemUUID = itemUUID;
                                    inventoryAssetUUID = assetUUID;
                                    UploadNotecardDataEvent.Set();
                                });
                            if (!UploadNotecardDataEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ITEM_DATA);
                            }
                        }
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