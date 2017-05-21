///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using Corrade.Constants;
using CorradeConfigurationSharp;
using OpenMetaverse;
using OpenMetaverse.Assets;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> updatenotecard
                =
                (corradeCommandParameters, result) =>
                {
                    byte[] data;
                    try
                    {
                        data = Convert.FromBase64String(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                    corradeCommandParameters.Message)));
                    }
                    catch (Exception)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                    }
                    InventoryItem inventoryItem = null;
                    var item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    var itemUUID = UUID.Zero;
                    var UploadNotecardAssetEvent = new ManualResetEvent(false);
                    bool succeeded = false;
                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Type>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                    corradeCommandParameters.Message))))
                    {
                        case Enumerations.Type.TASK:
                            if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int)Configuration.Permissions.Interact))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                            }
                            float range;
                            if (
                                !float.TryParse(
                                    wasInput(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                        corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                    out range))
                            {
                                range = corradeConfiguration.Range;
                            }
                            var entity =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                        corradeCommandParameters.Message));
                            UUID entityUUID;
                            if (!UUID.TryParse(entity, out entityUUID))
                            {
                                if (string.IsNullOrEmpty(entity))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                                }
                                entityUUID = UUID.Zero;
                            }
                            Primitive primitive = null;
                            if (string.IsNullOrEmpty(item))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                            }
                            switch (UUID.TryParse(item, out itemUUID))
                            {
                                case true:
                                    if (
                                        !Services.FindPrimitive(Client,
                                            itemUUID,
                                            range,
                                            ref primitive,
                                            corradeConfiguration.DataTimeout))
                                    {
                                        throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                                    }
                                    break;

                                default:
                                    if (
                                        !Services.FindPrimitive(Client,
                                            item,
                                            range,
                                            ref primitive,
                                            corradeConfiguration.DataTimeout))
                                    {
                                        throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                                    }
                                    break;
                            }
                            var inventory = new List<InventoryBase>();
                            Locks.ClientInstanceInventoryLock.EnterReadLock();
                            inventory.AddRange(
                                    Client.Inventory.GetTaskInventory(primitive.ID, primitive.LocalID,
                                        (int)corradeConfiguration.ServicesTimeout));
                            Locks.ClientInstanceInventoryLock.ExitReadLock();
                            inventoryItem = !entityUUID.Equals(UUID.Zero)
                                ? inventory.AsParallel().FirstOrDefault(o => o.UUID.Equals(entityUUID)) as InventoryItem
                                : inventory.AsParallel().FirstOrDefault(o => o.Name.Equals(entity)) as InventoryItem;
                            // If task inventory item does not exist create it.
                            succeeded = false;
                            if (inventoryItem == null ||
                                inventoryItem.AssetType.Equals(AssetType.Notecard))
                            {
                                var permissions = PermissionMask.None;
                                CSV.ToEnumerable(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PERMISSIONS)),
                                            corradeCommandParameters.Message)))
                                    .AsParallel()
                                    .Where(o => !string.IsNullOrEmpty(o))
                                    .ForAll(
                                        o => typeof(PermissionMask).GetFields(BindingFlags.Public | BindingFlags.Static)
                                            .AsParallel()
                                            .Where(p => string.Equals(o, p.Name, StringComparison.Ordinal))
                                            .ForAll(
                                                q =>
                                                {
                                                    BitTwiddling.SetMaskFlag(ref permissions, (PermissionMask)q.GetValue(null));
                                                }));

                                var name = wasInput(
                                    KeyValue.Get(
                                        wasOutput(
                                            Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                                        corradeCommandParameters.Message));
                                if (string.IsNullOrEmpty(name))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);
                                }
                                var CreateNotecardEvent = new ManualResetEvent(false);
                                Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(AssetType.Notecard),
                                        name,
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                                corradeCommandParameters.Message)),
                                        AssetType.Notecard,
                                        UUID.Random(), InventoryType.Notecard,
                                        permissions == 0 ? PermissionMask.Transfer : permissions,
                                        delegate (bool completed, InventoryItem createdItem)
                                        {
                                            inventoryItem = createdItem;
                                            succeeded = completed;
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
                                Client.Inventory.RequestUploadNotecardAsset(emptyNotecard.AssetData, inventoryItem.UUID,
                                    delegate (bool completed, string status, UUID itemID, UUID assetID)
                                    {
                                        succeeded = completed;
                                        inventoryItem.UUID = itemID;
                                        inventoryItem.AssetUUID = assetID;
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

                                // Copy the item to the task inventory.
                                Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                Client.Inventory.UpdateTaskInventory(primitive.LocalID, inventoryItem);
                                Client.Inventory.RemoveItem(inventoryItem.UUID);
                                Locks.ClientInstanceInventoryLock.ExitWriteLock();
                            }

                            // Update the notecard inside the task inventory.
                            Locks.ClientInstanceInventoryLock.EnterWriteLock();
                            Client.Inventory.RequestUpdateNotecardTask(data, inventoryItem.UUID, primitive.ID,
                                    delegate (bool completed, string status, UUID itemID, UUID assetID)
                                    {
                                        inventoryItem.UUID = itemID;
                                        inventoryItem.AssetUUID = assetID;
                                        succeeded = completed;
                                        UploadNotecardAssetEvent.Set();
                                    });
                            if (!UploadNotecardAssetEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, true))
                            {
                                Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ASSET);
                            }
                            Locks.ClientInstanceInventoryLock.ExitWriteLock();
                            break;

                        case Enumerations.Type.AGENT:
                            if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int)Configuration.Permissions.Inventory))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                            }
                            // If an item was specified then update instead of creating a new item for certain asset types.
                            switch (!string.IsNullOrEmpty(item))
                            {
                                case true:
                                    switch (UUID.TryParse(item, out itemUUID))
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
                                            inventoryItem = Inventory.FindInventory<InventoryBase>(Client, item,
                                                CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                                corradeConfiguration.ServicesTimeout) as InventoryItem;
                                            break;
                                    }

                                    if (inventoryItem == null || !inventoryItem.AssetType.Equals(AssetType.Notecard))
                                        throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                                    break;

                                default:
                                    var permissions = PermissionMask.None;
                                    CSV.ToEnumerable(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PERMISSIONS)),
                                                corradeCommandParameters.Message)))
                                        .AsParallel()
                                        .Where(o => !string.IsNullOrEmpty(o))
                                        .ForAll(
                                            o => typeof(PermissionMask).GetFields(BindingFlags.Public | BindingFlags.Static)
                                                .AsParallel()
                                                .Where(p => string.Equals(o, p.Name, StringComparison.Ordinal))
                                                .ForAll(
                                                    q =>
                                                    {
                                                        BitTwiddling.SetMaskFlag(ref permissions, (PermissionMask)q.GetValue(null));
                                                    }));

                                    var name = wasInput(KeyValue.Get(
                                        wasOutput(
                                            Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                                        corradeCommandParameters.Message));
                                    if (string.IsNullOrEmpty(name))
                                    {
                                        throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);
                                    }
                                    succeeded = false;
                                    var CreateNotecardEvent = new ManualResetEvent(false);
                                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                    Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(AssetType.Notecard),
                                            name,
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                                    corradeCommandParameters.Message)),
                                            AssetType.Notecard,
                                            UUID.Random(), InventoryType.Notecard,
                                            permissions == 0 ? PermissionMask.Transfer : permissions,
                                            delegate (bool completed, InventoryItem createdItem)
                                            {
                                                inventoryItem = createdItem;
                                                succeeded = completed;
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
                                    Client.Inventory.RequestUploadNotecardAsset(emptyNotecard.AssetData, inventoryItem.UUID,
                                        delegate (bool completed, string status, UUID itemID, UUID assetID)
                                        {
                                            succeeded = completed;
                                            inventoryItem.UUID = itemID;
                                            inventoryItem.AssetUUID = assetID;
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
                                    break;
                            }
                            Locks.ClientInstanceInventoryLock.EnterWriteLock();
                            Client.Inventory.RequestUploadNotecardAsset(data, inventoryItem.UUID,
                                    delegate (bool completed, string status, UUID itemID, UUID assetID)
                                    {
                                        succeeded = completed;
                                        inventoryItem.UUID = itemID;
                                        inventoryItem.AssetUUID = assetID;
                                        UploadNotecardAssetEvent.Set();
                                    });
                            if (!UploadNotecardAssetEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, true))
                            {
                                Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ASSET);
                            }
                            Locks.ClientInstanceInventoryLock.ExitWriteLock();
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_UPDATE_TYPE);
                    }

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
