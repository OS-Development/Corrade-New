///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Reflection;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using wasSharp;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> upload =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Inventory))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string name =
                        wasInput(KeyValue.wasKeyValueGet(wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                    {
                        throw new ScriptException(ScriptError.NO_NAME_PROVIDED);
                    }
                    uint permissions = 0;
                    Parallel.ForEach(CSV.wasCSVToEnumerable(
                        wasInput(
                            KeyValue.wasKeyValueGet(
                                wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.PERMISSIONS)),
                                corradeCommandParameters.Message))).AsParallel().Where(o => !string.IsNullOrEmpty(o)),
                        o =>
                            Parallel.ForEach(
                                typeof (PermissionMask).GetFields(BindingFlags.Public | BindingFlags.Static)
                                    .AsParallel().Where(p => p.Name.Equals(o, StringComparison.Ordinal)),
                                q => { permissions |= ((uint) q.GetValue(null)); }));
                    FieldInfo assetTypeInfo = typeof (AssetType).GetFields(BindingFlags.Public |
                                                                           BindingFlags.Static)
                        .AsParallel().FirstOrDefault(o =>
                            o.Name.Equals(
                                wasInput(
                                    KeyValue.wasKeyValueGet(
                                        Reflection.wasGetNameFromEnumValue(
                                            ScriptKeys.TYPE),
                                        corradeCommandParameters.Message)),
                                StringComparison.Ordinal));
                    if (assetTypeInfo == null)
                    {
                        throw new ScriptException(ScriptError.UNKNOWN_ASSET_TYPE);
                    }
                    AssetType assetType = (AssetType) assetTypeInfo.GetValue(null);
                    byte[] data;
                    try
                    {
                        data = Convert.FromBase64String(
                            wasInput(
                                KeyValue.wasKeyValueGet(wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.DATA)),
                                    corradeCommandParameters.Message)));
                    }
                    catch (Exception)
                    {
                        throw new ScriptException(ScriptError.INVALID_ASSET_DATA);
                    }
                    bool succeeded = false;
                    switch (assetType)
                    {
                        case AssetType.Texture:
                        case AssetType.Sound:
                        case AssetType.Animation:
                            // the holy asset trinity is charged money
                            if (
                                !HasCorradePermission(corradeCommandParameters.Group.Name,
                                    (int) Configuration.Permissions.Economy))
                            {
                                throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                            }
                            if (!UpdateBalance(corradeConfiguration.ServicesTimeout))
                            {
                                throw new ScriptException(ScriptError.UNABLE_TO_OBTAIN_MONEY_BALANCE);
                            }
                            if (Client.Self.Balance < Client.Settings.UPLOAD_COST)
                            {
                                throw new ScriptException(ScriptError.INSUFFICIENT_FUNDS);
                            }
                            switch (assetType)
                            {
                                case AssetType.Texture:
                                    // If the user did not send a JPEG-2000 Codestream, attempt to convert the data
                                    // and then encode to JPEG-2000 Codestream since that is what Second Life expects.
                                    ManagedImage managedImage;
                                    if (!OpenJPEG.DecodeToImage(data, out managedImage))
                                    {
                                        try
                                        {
                                            using (Image image = (Image) (new ImageConverter().ConvertFrom(data)))
                                            {
                                                using (Bitmap bitmap = new Bitmap(image))
                                                {
                                                    data = OpenJPEG.EncodeFromImage(bitmap, true);
                                                }
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            throw new Exception(
                                                Reflection.wasGetNameFromEnumValue(
                                                    ScriptError.UNKNOWN_IMAGE_FORMAT_PROVIDED));
                                        }
                                    }
                                    break;
                            }
                            // now create and upload the asset
                            ManualResetEvent CreateItemFromAssetEvent = new ManualResetEvent(false);
                            Client.Inventory.RequestCreateItemFromAsset(data, name,
                                wasInput(
                                    KeyValue.wasKeyValueGet(
                                        wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.DESCRIPTION)),
                                        corradeCommandParameters.Message)),
                                assetType,
                                (InventoryType)
                                    (typeof (InventoryType).GetFields(BindingFlags.Public | BindingFlags.Static)
                                        .AsParallel().FirstOrDefault(
                                            o => o.Name.Equals(Enum.GetName(typeof (AssetType), assetType),
                                                StringComparison.Ordinal))).GetValue(null),
                                Client.Inventory.FindFolderForType(assetType),
                                delegate(bool completed, string status, UUID itemID, UUID assetID)
                                {
                                    succeeded = completed;
                                    CreateItemFromAssetEvent.Set();
                                });
                            if (!CreateItemFromAssetEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                            {
                                throw new ScriptException(ScriptError.TIMEOUT_UPLOADING_ASSET);
                            }
                            break;
                        case AssetType.Bodypart:
                        case AssetType.Clothing:
                            FieldInfo wearTypeInfo = typeof (MuteType).GetFields(BindingFlags.Public |
                                                                                 BindingFlags.Static)
                                .AsParallel().FirstOrDefault(
                                    o =>
                                        o.Name.Equals(
                                            wasInput(
                                                KeyValue.wasKeyValueGet(
                                                    wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.WEAR)),
                                                    corradeCommandParameters.Message)),
                                            StringComparison.Ordinal));
                            if (wearTypeInfo == null)
                            {
                                throw new ScriptException(ScriptError.UNKNOWN_WEARABLE_TYPE);
                            }
                            UUID wearableUUID = Client.Assets.RequestUpload(assetType, data, false);
                            if (wearableUUID.Equals(UUID.Zero))
                            {
                                throw new ScriptException(ScriptError.ASSET_UPLOAD_FAILED);
                            }
                            ManualResetEvent CreateWearableEvent = new ManualResetEvent(false);
                            Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(assetType),
                                name,
                                wasInput(
                                    KeyValue.wasKeyValueGet(
                                        wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.DESCRIPTION)),
                                        corradeCommandParameters.Message)),
                                assetType,
                                wearableUUID, InventoryType.Wearable, (WearableType) wearTypeInfo.GetValue(null),
                                permissions == 0 ? PermissionMask.Transfer : (PermissionMask) permissions,
                                delegate(bool completed, InventoryItem createdItem)
                                {
                                    succeeded = completed;
                                    CreateWearableEvent.Set();
                                });
                            if (!CreateWearableEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                            {
                                throw new ScriptException(ScriptError.TIMEOUT_CREATING_ITEM);
                            }
                            break;
                        case AssetType.Landmark:
                            UUID landmarkUUID = Client.Assets.RequestUpload(assetType, data, false);
                            if (landmarkUUID.Equals(UUID.Zero))
                            {
                                throw new ScriptException(ScriptError.ASSET_UPLOAD_FAILED);
                            }
                            ManualResetEvent CreateLandmarkEvent = new ManualResetEvent(false);
                            Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(assetType),
                                name,
                                wasInput(
                                    KeyValue.wasKeyValueGet(
                                        wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.DESCRIPTION)),
                                        corradeCommandParameters.Message)),
                                assetType,
                                landmarkUUID, InventoryType.Landmark, PermissionMask.All,
                                delegate(bool completed, InventoryItem createdItem)
                                {
                                    succeeded = completed;
                                    CreateLandmarkEvent.Set();
                                });
                            if (!CreateLandmarkEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                            {
                                throw new ScriptException(ScriptError.TIMEOUT_CREATING_ITEM);
                            }
                            break;
                        case AssetType.Gesture:
                            ManualResetEvent CreateGestureEvent = new ManualResetEvent(false);
                            InventoryItem newGesture = null;
                            Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(assetType),
                                name,
                                wasInput(
                                    KeyValue.wasKeyValueGet(
                                        wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.DESCRIPTION)),
                                        corradeCommandParameters.Message)),
                                assetType,
                                UUID.Random(), InventoryType.Gesture,
                                permissions == 0 ? PermissionMask.Transfer : (PermissionMask) permissions,
                                delegate(bool completed, InventoryItem createdItem)
                                {
                                    succeeded = completed;
                                    newGesture = createdItem;
                                    CreateGestureEvent.Set();
                                });
                            if (!CreateGestureEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                            {
                                throw new ScriptException(ScriptError.TIMEOUT_CREATING_ITEM);
                            }
                            if (!succeeded)
                            {
                                throw new ScriptException(ScriptError.UNABLE_TO_CREATE_ITEM);
                            }
                            ManualResetEvent UploadGestureAssetEvent = new ManualResetEvent(false);
                            Client.Inventory.RequestUploadGestureAsset(data, newGesture.UUID,
                                delegate(bool completed, string status, UUID itemUUID, UUID assetUUID)
                                {
                                    succeeded = completed;
                                    UploadGestureAssetEvent.Set();
                                });
                            if (!UploadGestureAssetEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                            {
                                throw new ScriptException(ScriptError.TIMEOUT_UPLOADING_ASSET);
                            }
                            break;
                        case AssetType.Notecard:
                            ManualResetEvent CreateNotecardEvent = new ManualResetEvent(false);
                            InventoryItem newNotecard = null;
                            Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(assetType),
                                name,
                                wasInput(
                                    KeyValue.wasKeyValueGet(
                                        wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.DESCRIPTION)),
                                        corradeCommandParameters.Message)),
                                assetType,
                                UUID.Random(), InventoryType.Notecard,
                                permissions == 0 ? PermissionMask.Transfer : (PermissionMask) permissions,
                                delegate(bool completed, InventoryItem createdItem)
                                {
                                    succeeded = completed;
                                    newNotecard = createdItem;
                                    CreateNotecardEvent.Set();
                                });
                            if (!CreateNotecardEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                            {
                                throw new ScriptException(ScriptError.TIMEOUT_CREATING_ITEM);
                            }
                            if (!succeeded)
                            {
                                throw new ScriptException(ScriptError.UNABLE_TO_CREATE_ITEM);
                            }
                            ManualResetEvent UploadNotecardAssetEvent = new ManualResetEvent(false);
                            Client.Inventory.RequestUploadNotecardAsset(data, newNotecard.UUID,
                                delegate(bool completed, string status, UUID itemUUID, UUID assetUUID)
                                {
                                    succeeded = completed;
                                    UploadNotecardAssetEvent.Set();
                                });
                            if (!UploadNotecardAssetEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                            {
                                throw new ScriptException(ScriptError.TIMEOUT_UPLOADING_ASSET);
                            }
                            break;
                        case AssetType.LSLText:
                            ManualResetEvent CreateScriptEvent = new ManualResetEvent(false);
                            InventoryItem newScript = null;
                            Client.Inventory.RequestCreateItem(Client.Inventory.FindFolderForType(assetType),
                                name,
                                wasInput(
                                    KeyValue.wasKeyValueGet(
                                        wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.DESCRIPTION)),
                                        corradeCommandParameters.Message)),
                                assetType,
                                UUID.Random(), InventoryType.LSL,
                                permissions == 0 ? PermissionMask.Transfer : (PermissionMask) permissions,
                                delegate(bool completed, InventoryItem createdItem)
                                {
                                    succeeded = completed;
                                    newScript = createdItem;
                                    CreateScriptEvent.Set();
                                });
                            if (!CreateScriptEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                            {
                                throw new ScriptException(ScriptError.TIMEOUT_CREATING_ITEM);
                            }
                            ManualResetEvent UpdateScriptEvent = new ManualResetEvent(false);
                            Client.Inventory.RequestUpdateScriptAgentInventory(data, newScript.UUID, true,
                                delegate(bool completed, string status, bool compiled, List<string> messages,
                                    UUID itemID, UUID assetID)
                                {
                                    succeeded = completed;
                                    UpdateScriptEvent.Set();
                                });
                            if (!UpdateScriptEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                            {
                                throw new ScriptException(ScriptError.TIMEOUT_UPLOADING_ASSET);
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_INVENTORY_TYPE);
                    }
                    if (!succeeded)
                    {
                        throw new ScriptException(ScriptError.ASSET_UPLOAD_FAILED);
                    }
                };
        }
    }
}