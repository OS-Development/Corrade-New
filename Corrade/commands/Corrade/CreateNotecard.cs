///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Corrade.Constants;
using CorradeConfigurationSharp;
using OpenMetaverse;
using OpenMetaverse.Assets;
using wasOpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;
using Parallel = System.Threading.Tasks.Parallel;
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
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                    var permissions = Permissions.NoPermissions;
                    Inventory.wasStringToPermissions(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PERMISSIONS)),
                            corradeCommandParameters.Message)), out permissions);

                    // Check all inventory items.
                    var LockObject = new object();
                    var error = Enumerations.ScriptError.NONE;
                    var attachments = new List<InventoryItem>();
                    Parallel.ForEach(CSV.ToEnumerable(wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ATTACHMENTS)),
                        corradeCommandParameters.Message))).Where(o => !string.IsNullOrEmpty(o)), (o, s) =>
                    {
                        InventoryItem attachmentItem = null;
                        UUID attachmentUUID;
                        switch (UUID.TryParse(o, out attachmentUUID))
                        {
                            case true:
                                Locks.ClientInstanceInventoryLock.EnterReadLock();
                                if (Client.Inventory.Store.Contains(attachmentUUID))
                                    attachmentItem = Client.Inventory.Store[attachmentUUID] as InventoryItem;
                                Locks.ClientInstanceInventoryLock.ExitReadLock();
                                break;

                            default:
                                attachmentItem =
                                    Inventory.FindInventory<InventoryItem>(Client, o,
                                        CORRADE_CONSTANTS.PATH_SEPARATOR,
                                        CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                        corradeConfiguration.ServicesTimeout);
                                break;
                        }

                        if (attachmentItem == null)
                        {
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), o);
                            error = Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND;
                            s.Break();
                            return;
                        }

                        if (!attachmentItem.Permissions.NextOwnerMask
                            .HasFlag(PermissionMask.Modify | PermissionMask.Copy | PermissionMask.Transfer))
                        {
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), o);
                            error = Enumerations.ScriptError.NO_PERMISSIONS_FOR_ITEM;
                            s.Break();
                            return;
                        }

                        lock (LockObject)
                        {
                            attachments.Add(attachmentItem);
                        }
                    });

                    if (!error.Equals(Enumerations.ScriptError.NONE))
                        throw new Command.ScriptException(error);

                    var name =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);

                    // Create notecard.
                    var CreateNotecardEvent = new ManualResetEventSlim(false);
                    var inventoryItem = new InventoryItem(UUID.Zero);
                    var succeeded = false;
                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                    Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(AssetType.Notecard),
                        name,
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                corradeCommandParameters.Message)),
                        AssetType.Notecard,
                        UUID.Random(),
                        InventoryType.Notecard,
                        permissions.Equals(Permissions.NoPermissions)
                            ? PermissionMask.Transfer
                            : permissions.NextOwnerMask,
                        delegate(bool completed, InventoryItem createdItem)
                        {
                            succeeded = completed;
                            inventoryItem = createdItem;
                            CreateNotecardEvent.Set();
                        });
                    if (!CreateNotecardEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_CREATING_ITEM);
                    }
                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                    if (!succeeded)
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_CREATE_ITEM);

                    // Upload blank notecard.
                    var emptyNotecard = new AssetNotecard
                    {
                        BodyText = wasOpenMetaverse.Constants.ASSETS.NOTECARD.NEWLINE
                    };
                    emptyNotecard.Encode();

                    var UploadNotecardAssetEvent = new ManualResetEventSlim(false);
                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                    Client.Inventory.RequestUploadNotecardAsset(emptyNotecard.AssetData, inventoryItem.UUID,
                        delegate(bool completed, string status, UUID itemUUID, UUID assetUUID)
                        {
                            succeeded = completed;
                            inventoryItem.UUID = itemUUID;
                            inventoryItem.AssetUUID = assetUUID;
                            UploadNotecardAssetEvent.Set();
                        });
                    if (!UploadNotecardAssetEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_CREATING_ITEM);
                    }
                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                    if (!succeeded)
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_CREATE_ITEM);

                    var temporary = false;
                    if (!bool.TryParse(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TEMPORARY)),
                            corradeCommandParameters.Message)), out temporary))
                        temporary = false;

                    AssetNotecard notecard = null;
                    switch (Reflection.GetEnumValueFromName<Enumerations.Entity>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message))))
                    {
                        case Enumerations.Entity.FILE:
                            if (
                                !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                    (int) Configuration.Permissions.System))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                            var path =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PATH)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(path))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_PATH_PROVIDED);
                            // Read from file.
                            var data = string.Empty;
                            try
                            {
                                using (
                                    var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read,
                                        FileShare.Read,
                                        16384, true))
                                {
                                    using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                                    {
                                        data = streamReader.ReadToEnd();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), ex.Message);
                                throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_READ_FILE);
                            }
                            notecard = new AssetNotecard(inventoryItem.AssetUUID, null)
                            {
                                BodyText = data,
                                EmbeddedItems = attachments,
                                Temporary = temporary
                            };
                            break;

                        case Enumerations.Entity.TEXT:
                            var text =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(text))
                                data = wasOpenMetaverse.Constants.ASSETS.NOTECARD.NEWLINE;

                            notecard = new AssetNotecard(inventoryItem.AssetUUID, null)
                            {
                                BodyText = text,
                                EmbeddedItems = attachments,
                                Temporary = temporary
                            };
                            break;

                        case Enumerations.Entity.ASSET:
                            byte[] asset = null;
                            try
                            {
                                asset = Convert.FromBase64String(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                            corradeCommandParameters.Message)));
                            }
                            catch (Exception)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                            }

                            if (asset == null || asset.Length.Equals(0))
                                throw new Command.ScriptException(Enumerations.ScriptError.EMPTY_ASSET_DATA);

                            var assetNotecard = new AssetNotecard
                            {
                                AssetData = asset
                            };

                            if (!assetNotecard.Decode())
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);

                            notecard = new AssetNotecard(inventoryItem.AssetUUID, null)
                            {
                                BodyText = assetNotecard.BodyText,
                                EmbeddedItems = attachments,
                                Temporary = temporary
                            };
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }

                    notecard.Encode();

                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                        Encoding.UTF8.GetByteCount(notecard.BodyText) >
                        wasOpenMetaverse.Constants.ASSETS.NOTECARD.MAXIMUM_BODY_LENTH)
                        throw new Command.ScriptException(Enumerations.ScriptError.NOTECARD_MESSAGE_BODY_TOO_LARGE);

                    succeeded = false;
                    UploadNotecardAssetEvent.Reset();
                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                    Client.Inventory.RequestUploadNotecardAsset(notecard.AssetData, inventoryItem.UUID,
                        delegate(bool completed, string status, UUID itemUUID, UUID assetUUID)
                        {
                            succeeded = completed;
                            inventoryItem.UUID = itemUUID;
                            inventoryItem.AssetUUID = assetUUID;
                            UploadNotecardAssetEvent.Set();
                        });
                    if (!UploadNotecardAssetEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ITEM_DATA);
                    }
                    Locks.ClientInstanceInventoryLock.ExitWriteLock();

                    if (!succeeded)
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_UPLOAD_ITEM_DATA);

                    // Mark the containing asset folder as needing an update.
                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                    Client.Inventory.Store.GetNodeFor(Client.Inventory.FindFolderForType(AssetType.Notecard))
                        .NeedsUpdate = true;
                    Locks.ClientInstanceInventoryLock.ExitWriteLock();

                    // Return the item and asset UUID.
                    result.Add(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                        CSV.FromEnumerable(new[]
                        {
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                            inventoryItem.UUID.ToString(),
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ASSET)),
                            inventoryItem.AssetUUID.ToString()
                        }));
                };
        }
    }
}