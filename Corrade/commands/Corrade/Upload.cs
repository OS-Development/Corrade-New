///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Threading;
using CorradeConfiguration;
using ImageMagick;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using wasOpenMetaverse;
using wasSharp;
using Graphics = wasOpenMetaverse.Graphics;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Command.CorradeCommandParameters, Dictionary<string, string>> upload =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var name =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);
                    }
                    var permissions = PermissionMask.None;
                    CSV.ToEnumerable(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PERMISSIONS)),
                                corradeCommandParameters.Message)))
                        .ToArray()
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o))
                        .ForAll(
                            o => typeof (PermissionMask).GetFields(BindingFlags.Public | BindingFlags.Static)
                                .AsParallel().Where(p => Strings.Equals(o, p.Name, StringComparison.Ordinal)).ForAll(
                                    q =>
                                    {
                                        BitTwiddling.SetMaskFlag(ref permissions, (PermissionMask) q.GetValue(null));
                                    }));
                    var assetTypeInfo = typeof (AssetType).GetFields(BindingFlags.Public |
                                                                     BindingFlags.Static)
                        .AsParallel().FirstOrDefault(o =>
                            o.Name.Equals(
                                wasInput(
                                    KeyValue.Get(
                                        Reflection.GetNameFromEnumValue(
                                            Command.ScriptKeys.TYPE),
                                        corradeCommandParameters.Message)),
                                StringComparison.Ordinal));
                    if (assetTypeInfo == null)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ASSET_TYPE);
                    }
                    var assetType = (AssetType) assetTypeInfo.GetValue(null);
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
                    var succeeded = false;
                    var assetUUID = UUID.Zero;
                    var itemUUID = UUID.Zero;
                    switch (assetType)
                    {
                        case AssetType.Texture:
                        case AssetType.Animation:
                            // the holy asset trinity is charged money
                            if (
                                !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                    (int) Configuration.Permissions.Economy))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                            }
                            if (!Services.UpdateBalance(Client, corradeConfiguration.ServicesTimeout))
                            {
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.UNABLE_TO_OBTAIN_MONEY_BALANCE);
                            }
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                if (Client.Self.Balance < Client.Settings.UPLOAD_COST)
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.INSUFFICIENT_FUNDS);
                                }
                            }
                            switch (assetType)
                            {
                                case AssetType.Texture:
                                    ManagedImage managedImage;
                                    // If the user did not send a JPEG-2000 Codestream, attempt to convert the data
                                    // and then encode to JPEG-2000 Codestream since that is what Second Life expects.
                                    if (!OpenJPEG.DecodeToImage(data, out managedImage))
                                    {
                                        /*
                                         * Use ImageMagick on Windows and the .NET converter otherwise.
                                         */
                                        switch (Environment.OSVersion.Platform)
                                        {
                                            case PlatformID.Win32NT:
                                                try
                                                {
                                                    using (var magickImage = new MagickImage(data))
                                                    {
                                                        using (var image = new Bitmap(magickImage.ToBitmap()))
                                                        {
                                                            var size = Graphics.GetScaleTextureSize(
                                                                image.Width, image.Height);

                                                            using (
                                                                var newImage = new Bitmap(size.Width, size.Height,
                                                                    PixelFormat.Format24bppRgb))
                                                            {
                                                                using (
                                                                    var g =
                                                                        System.Drawing.Graphics.FromImage(newImage))
                                                                {
                                                                    g.SmoothingMode =
                                                                        SmoothingMode
                                                                            .HighQuality;
                                                                    g.InterpolationMode =
                                                                        InterpolationMode
                                                                            .HighQualityBicubic;
                                                                    g.DrawImage(image, 0, 0, size.Width, size.Height);
                                                                    data = OpenJPEG.EncodeFromImage(newImage, true);
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                                catch (Exception)
                                                {
                                                    throw new Command.ScriptException(
                                                        Enumerations.ScriptError.UNKNOWN_IMAGE_FORMAT_PROVIDED);
                                                }

                                                break;
                                            default:
                                                try
                                                {
                                                    using (var imageByteStream = new MemoryStream(data))
                                                    {
                                                        using (var image = Image.FromStream(imageByteStream))
                                                        {
                                                            var size = Graphics.GetScaleTextureSize(
                                                                image.Width, image.Height);

                                                            using (
                                                                var newImage = new Bitmap(size.Width, size.Height,
                                                                    PixelFormat.Format24bppRgb))
                                                            {
                                                                using (
                                                                    var g = System.Drawing.Graphics.FromImage(newImage))
                                                                {
                                                                    g.SmoothingMode =
                                                                        SmoothingMode
                                                                            .HighQuality;
                                                                    g.InterpolationMode =
                                                                        InterpolationMode
                                                                            .HighQualityBicubic;
                                                                    g.DrawImage(image, 0, 0, size.Width, size.Height);
                                                                    data = OpenJPEG.EncodeFromImage(newImage, true);
                                                                }
                                                            }
                                                        }
                                                    }
                                                }
                                                catch (Exception)
                                                {
                                                    throw new Command.ScriptException(
                                                        Enumerations.ScriptError.UNKNOWN_IMAGE_FORMAT_PROVIDED);
                                                }
                                                break;
                                        }
                                    }
                                    break;
                            }
                            // ...now create and upload the asset
                            var CreateItemFromAssetEvent = new ManualResetEvent(false);
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.RequestCreateItemFromAsset(data, name,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                            corradeCommandParameters.Message)),
                                    assetType,
                                    (InventoryType)
                                        typeof (InventoryType).GetFields(BindingFlags.Public | BindingFlags.Static)
                                            .AsParallel().FirstOrDefault(
                                                o => o.Name.Equals(Enum.GetName(typeof (AssetType), assetType),
                                                    StringComparison.Ordinal)).GetValue(null),
                                    Client.Inventory.FindFolderForType(assetType),
                                    delegate(bool completed, string status, UUID itemID, UUID assetID)
                                    {
                                        itemUUID = itemID;
                                        assetUUID = assetID;
                                        succeeded = completed;
                                        CreateItemFromAssetEvent.Set();
                                    });
                                if (!CreateItemFromAssetEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ASSET);
                                }
                            }
                            break;
                        case AssetType.SoundWAV:
                        case AssetType.Sound:
                            UUID soundUUID;
                            lock (Locks.ClientInstanceAssetsLock)
                            {
                                soundUUID = Client.Assets.RequestUpload(assetType, data, false);
                            }
                            if (soundUUID.Equals(UUID.Zero))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.ASSET_UPLOAD_FAILED);
                            }
                            var CreateSoundEvent = new ManualResetEvent(false);
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.RequestCreateItemFromAsset(data, name,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                            corradeCommandParameters.Message)),
                                    assetType,
                                    (InventoryType)
                                        typeof (InventoryType).GetFields(BindingFlags.Public | BindingFlags.Static)
                                            .AsParallel().FirstOrDefault(
                                                o => o.Name.Equals(Enum.GetName(typeof (AssetType), assetType),
                                                    StringComparison.Ordinal)).GetValue(null),
                                    Client.Inventory.FindFolderForType(assetType),
                                    delegate(bool completed, string status, UUID itemID, UUID assetID)
                                    {
                                        itemUUID = itemID;
                                        assetUUID = assetID;
                                        succeeded = completed;
                                        CreateSoundEvent.Set();
                                    });
                                if (!CreateSoundEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_CREATING_ITEM);
                                }
                            }
                            break;
                        case AssetType.Bodypart:
                        case AssetType.Clothing:
                            var wearTypeInfo = typeof (MuteType).GetFields(BindingFlags.Public |
                                                                           BindingFlags.Static)
                                .AsParallel().FirstOrDefault(
                                    o =>
                                        o.Name.Equals(
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.WEAR)),
                                                    corradeCommandParameters.Message)),
                                            StringComparison.Ordinal));
                            if (wearTypeInfo == null)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_WEARABLE_TYPE);
                            }
                            var wearableUUID = UUID.Zero;
                            lock (Locks.ClientInstanceAssetsLock)
                            {
                                wearableUUID = Client.Assets.RequestUpload(assetType, data, false);
                                if (wearableUUID.Equals(UUID.Zero))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.ASSET_UPLOAD_FAILED);
                                }
                            }
                            var CreateWearableEvent = new ManualResetEvent(false);
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(assetType),
                                    name,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                            corradeCommandParameters.Message)),
                                    assetType,
                                    wearableUUID, InventoryType.Wearable, (WearableType) wearTypeInfo.GetValue(null),
                                    permissions == 0 ? PermissionMask.Transfer : permissions,
                                    delegate(bool completed, InventoryItem createdItem)
                                    {
                                        assetUUID = createdItem.AssetUUID;
                                        itemUUID = createdItem.UUID;
                                        succeeded = completed;
                                        CreateWearableEvent.Set();
                                    });
                                if (!CreateWearableEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_CREATING_ITEM);
                                }
                            }
                            break;
                        case AssetType.Landmark:
                            var landmarkUUID = Client.Assets.RequestUpload(assetType, data, false);
                            if (landmarkUUID.Equals(UUID.Zero))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.ASSET_UPLOAD_FAILED);
                            }
                            var CreateLandmarkEvent = new ManualResetEvent(false);
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(assetType),
                                    name,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                            corradeCommandParameters.Message)),
                                    assetType,
                                    landmarkUUID, InventoryType.Landmark, PermissionMask.All,
                                    delegate(bool completed, InventoryItem createdItem)
                                    {
                                        assetUUID = createdItem.AssetUUID;
                                        itemUUID = createdItem.UUID;
                                        succeeded = completed;
                                        CreateLandmarkEvent.Set();
                                    });
                                if (!CreateLandmarkEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_CREATING_ITEM);
                                }
                            }
                            break;
                        case AssetType.Gesture:
                            var CreateGestureEvent = new ManualResetEvent(false);
                            InventoryItem newGesture = null;
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(assetType),
                                    name,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                            corradeCommandParameters.Message)),
                                    assetType,
                                    UUID.Random(), InventoryType.Gesture,
                                    permissions == 0 ? PermissionMask.Transfer : permissions,
                                    delegate(bool completed, InventoryItem createdItem)
                                    {
                                        assetUUID = createdItem.AssetUUID;
                                        itemUUID = createdItem.UUID;
                                        succeeded = completed;
                                        newGesture = createdItem;
                                        CreateGestureEvent.Set();
                                    });
                                if (!CreateGestureEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_CREATING_ITEM);
                                }
                            }
                            if (!succeeded)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_CREATE_ITEM);
                            }
                            var UploadGestureAssetEvent = new ManualResetEvent(false);
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.RequestUploadGestureAsset(data, newGesture.UUID,
                                    delegate(bool completed, string status, UUID itemID, UUID assetID)
                                    {
                                        assetUUID = assetID;
                                        itemUUID = itemID;
                                        succeeded = completed;
                                        UploadGestureAssetEvent.Set();
                                    });
                                if (!UploadGestureAssetEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ASSET);
                                }
                            }
                            break;
                        case AssetType.Notecard:
                            var CreateNotecardEvent = new ManualResetEvent(false);
                            InventoryItem newNotecard = null;
                            lock (Locks.ClientInstanceNetworkLock)
                            {
                                Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(assetType),
                                    name,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                            corradeCommandParameters.Message)),
                                    assetType,
                                    UUID.Random(), InventoryType.Notecard,
                                    permissions == 0 ? PermissionMask.Transfer : permissions,
                                    delegate(bool completed, InventoryItem createdItem)
                                    {
                                        assetUUID = createdItem.AssetUUID;
                                        itemUUID = createdItem.UUID;
                                        succeeded = completed;
                                        newNotecard = createdItem;
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
                            var UploadNotecardAssetEvent = new ManualResetEvent(false);
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.RequestUploadNotecardAsset(data, newNotecard.UUID,
                                    delegate(bool completed, string status, UUID itemID, UUID assetID)
                                    {
                                        assetUUID = assetID;
                                        itemUUID = itemID;
                                        succeeded = completed;
                                        UploadNotecardAssetEvent.Set();
                                    });
                                if (!UploadNotecardAssetEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ASSET);
                                }
                            }
                            break;
                        case AssetType.LSLText:
                            var CreateScriptEvent = new ManualResetEvent(false);
                            InventoryItem newScript = null;
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(assetType),
                                    name,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                            corradeCommandParameters.Message)),
                                    assetType,
                                    UUID.Random(), InventoryType.LSL,
                                    permissions == 0 ? PermissionMask.Transfer : permissions,
                                    delegate(bool completed, InventoryItem createdItem)
                                    {
                                        assetUUID = createdItem.AssetUUID;
                                        itemUUID = createdItem.UUID;
                                        succeeded = completed;
                                        newScript = createdItem;
                                        CreateScriptEvent.Set();
                                    });
                                if (!CreateScriptEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_CREATING_ITEM);
                                }
                            }
                            var UpdateScriptEvent = new ManualResetEvent(false);
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.RequestUpdateScriptAgentInventory(data, newScript.UUID, true,
                                    delegate(bool completed, string status, bool compiled, List<string> messages,
                                        UUID itemID, UUID assetID)
                                    {
                                        assetUUID = assetID;
                                        itemUUID = itemID;
                                        succeeded = completed;
                                        UpdateScriptEvent.Set();
                                    });
                                if (!UpdateScriptEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ASSET);
                                }
                            }
                            break;
                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_INVENTORY_TYPE);
                    }
                    if (!succeeded)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.ASSET_UPLOAD_FAILED);
                    }
                    // Store the any asset in the cache.
                    if (!assetUUID.Equals(UUID.Zero))
                    {
                        lock (Locks.ClientInstanceAssetsLock)
                        {
                            Client.Assets.Cache.SaveAssetToCache(assetUUID, data);
                        }
                        if (corradeConfiguration.EnableHorde)
                            HordeDistributeCacheAsset(itemUUID, data, Configuration.HordeDataSynchronizationOption.Add);
                    }
                    // Return the item and asset UUID.
                    result.Add(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                        CSV.FromEnumerable(new[]
                        {
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)), itemUUID.ToString(),
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ASSET)), assetUUID.ToString()
                        }));
                };
        }
    }
}