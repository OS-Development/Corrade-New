///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Corrade.Constants;
using CorradeConfigurationSharp;
using OpenMetaverse;
using OpenMetaverse.Assets;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getassetdata
                =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int) Configuration.Permissions.Interact))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var item = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                    var assetTypeInfo = typeof(AssetType).GetFields(BindingFlags.Public |
                                                                    BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                            corradeCommandParameters.Message)),
                                    StringComparison.Ordinal));
                    switch (assetTypeInfo != null)
                    {
                        case false:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ASSET_TYPE);
                    }
                    var assetType = (AssetType) assetTypeInfo.GetValue(null);
                    var inventoryItem = new InventoryItem(UUID.Zero);
                    UUID itemUUID;
                    // If the asset is of an asset type that can only be retrieved locally or the item is a string
                    // then attempt to resolve the item to an inventory item or else the item cannot be found.
                    if (!UUID.TryParse(item, out itemUUID))
                    {
                        inventoryItem =
                            Inventory.FindInventory<InventoryItem>(Client, item,
                                CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                corradeConfiguration.ServicesTimeout);
                        if (inventoryItem == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                        itemUUID = inventoryItem.AssetUUID;
                    }
                    byte[] assetData = null;
                    Locks.ClientInstanceAssetsLock.EnterReadLock();
                    var cacheHasAsset = Client.Assets.Cache.HasAsset(itemUUID);
                    Locks.ClientInstanceAssetsLock.ExitReadLock();
                    switch (!cacheHasAsset)
                    {
                        case true:
                            var RequestAssetEvent = new ManualResetEventSlim(false);
                            var succeeded = false;
                            switch (assetType)
                            {
                                case AssetType.Mesh:
                                    Locks.ClientInstanceAssetsLock.EnterReadLock();
                                    Client.Assets.RequestMesh(itemUUID,
                                        delegate(bool completed, AssetMesh asset)
                                        {
                                            if (!asset.AssetID.Equals(itemUUID)) return;
                                            succeeded = completed;
                                            if (succeeded)
                                                assetData = asset.MeshData.AsBinary();
                                            RequestAssetEvent.Set();
                                        });
                                    if (
                                        !RequestAssetEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                                    {
                                        Locks.ClientInstanceAssetsLock.ExitReadLock();
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.TIMEOUT_TRANSFERRING_ASSET);
                                    }
                                    Locks.ClientInstanceAssetsLock.ExitReadLock();
                                    break;
                                // All of these can only be fetched if they exist locally.
                                case AssetType.LSLText:
                                case AssetType.Notecard:
                                    if (
                                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                            (int) Configuration.Permissions.Inventory))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                                    if (inventoryItem == null || inventoryItem.UUID.Equals(UUID.Zero))
                                    {
                                        Locks.ClientInstanceInventoryLock.EnterReadLock();
                                        if (Client.Inventory.Store.Contains(itemUUID))
                                            inventoryItem = Client.Inventory.Store[itemUUID] as InventoryItem;
                                        Locks.ClientInstanceInventoryLock.ExitReadLock();
                                        if (inventoryItem == null)
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                                    }
                                    Locks.ClientInstanceAssetsLock.EnterReadLock();
                                    Client.Assets.RequestInventoryAsset(inventoryItem, true,
                                        delegate(AssetDownload transfer, Asset asset)
                                        {
                                            succeeded = transfer.Success;
                                            if (transfer.Success)
                                                assetData = asset.AssetData;
                                            RequestAssetEvent.Set();
                                        });
                                    if (
                                        !RequestAssetEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                                    {
                                        Locks.ClientInstanceAssetsLock.ExitReadLock();
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.TIMEOUT_TRANSFERRING_ASSET);
                                    }
                                    Locks.ClientInstanceAssetsLock.ExitReadLock();
                                    break;
                                // All images go through RequestImage and can be fetched directly from the asset server.
                                case AssetType.Texture:
                                    Locks.ClientInstanceAssetsLock.EnterReadLock();
                                    Client.Assets.RequestImage(itemUUID, ImageType.Normal,
                                        delegate(TextureRequestState state, AssetTexture asset)
                                        {
                                            if (!asset.AssetID.Equals(itemUUID)) return;
                                            if (!state.Equals(TextureRequestState.Finished)) return;
                                            assetData = asset.AssetData;
                                            succeeded = true;
                                            RequestAssetEvent.Set();
                                        });
                                    if (
                                        !RequestAssetEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                                    {
                                        Locks.ClientInstanceAssetsLock.ExitReadLock();
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.TIMEOUT_TRANSFERRING_ASSET);
                                    }
                                    Locks.ClientInstanceAssetsLock.ExitReadLock();
                                    break;
                                // All of these can be fetched directly from the asset server.
                                case AssetType.Landmark:
                                case AssetType.Gesture:
                                case AssetType.Animation: // Animatn
                                case AssetType.Sound: // Ogg Vorbis
                                case AssetType.Clothing:
                                case AssetType.Bodypart:
                                    Locks.ClientInstanceAssetsLock.EnterReadLock();
                                    Client.Assets.RequestAsset(itemUUID, assetType, true,
                                        delegate(AssetDownload transfer, Asset asset)
                                        {
                                            if (!transfer.AssetID.Equals(itemUUID)) return;
                                            succeeded = transfer.Success;
                                            if (transfer.Success)
                                                assetData = asset.AssetData;
                                            RequestAssetEvent.Set();
                                        });
                                    if (!RequestAssetEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                                    {
                                        Locks.ClientInstanceAssetsLock.ExitReadLock();
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.TIMEOUT_TRANSFERRING_ASSET);
                                    }
                                    Locks.ClientInstanceAssetsLock.ExitReadLock();
                                    break;

                                default:
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ASSET_TYPE);
                            }
                            if (!succeeded)
                                throw new Command.ScriptException(Enumerations.ScriptError.FAILED_TO_DOWNLOAD_ASSET);
                            if (corradeConfiguration.EnableHorde)
                                HordeDistributeCacheAsset(itemUUID, assetData,
                                    Configuration.HordeDataSynchronizationOption.Add);
                            break;

                        default:
                            Locks.ClientInstanceAssetsLock.EnterReadLock();
                            assetData = Client.Assets.Cache.GetCachedAssetBytes(itemUUID);
                            Locks.ClientInstanceAssetsLock.ExitReadLock();
                            break;
                    }

                    var data = new List<string>();
                    switch (inventoryItem.AssetType)
                    {
                        case AssetType.Mesh:
                            var assetMesh = new AssetMesh
                            {
                                AssetData = assetData
                            };
                            if (!assetMesh.Decode())
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                            data.AddRange(assetMesh.GetStructuredData(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))));
                            break;

                        case AssetType.LSLText:
                            var assetLSL = new AssetScriptText
                            {
                                AssetData = assetData
                            };
                            if (!assetLSL.Decode())
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                            data.AddRange(assetLSL.GetStructuredData(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))));
                            break;

                        case AssetType.LSLBytecode:
                            var assetBytecode = new AssetScriptBinary
                            {
                                AssetData = assetData
                            };
                            if (!assetBytecode.Decode())
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                            data.AddRange(assetBytecode.GetStructuredData(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))));
                            break;

                        case AssetType.Notecard:
                            var assetNotecard = new AssetNotecard
                            {
                                AssetData = assetData
                            };
                            if (!assetNotecard.Decode())
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                            data.AddRange(assetNotecard.GetStructuredData(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))));
                            break;

                        case AssetType.Texture:
                            var assetTexture = new AssetTexture
                            {
                                AssetData = assetData
                            };
                            if (!assetTexture.Decode())
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                            data.AddRange(assetTexture.GetStructuredData(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))));
                            break;

                        case AssetType.Landmark:
                            var assetLandmark = new AssetLandmark
                            {
                                AssetData = assetData
                            };
                            if (!assetLandmark.Decode())
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                            data.AddRange(assetLandmark.GetStructuredData(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))));
                            break;

                        case AssetType.Gesture:
                            var assetGesture = new AssetGesture
                            {
                                AssetData = assetData
                            };
                            if (!assetGesture.Decode())
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                            data.AddRange(assetGesture.GetStructuredData(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))));
                            break;

                        case AssetType.Animation: // Animatn
                            var assetAnimation = new AssetAnimation
                            {
                                AssetData = assetData
                            };
                            if (!assetAnimation.Decode())
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                            data.AddRange(assetAnimation.GetStructuredData(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))));
                            break;

                        case AssetType.Sound: // Ogg Vorbis
                            var assetSound = new AssetSound
                            {
                                AssetData = assetData
                            };
                            if (!assetSound.Decode())
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                            data.AddRange(assetSound.GetStructuredData(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))));
                            break;

                        case AssetType.Clothing:
                            var assetClothing = new AssetClothing
                            {
                                AssetData = assetData
                            };
                            if (!assetClothing.Decode())
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                            data.AddRange(assetClothing.GetStructuredData(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))));
                            break;

                        case AssetType.Bodypart:
                            var assetBodypart = new AssetBodypart
                            {
                                AssetData = assetData
                            };
                            if (!assetBodypart.Decode())
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                            data.AddRange(assetBodypart.GetStructuredData(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))));
                            break;
                    }

                    if (data.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                };
        }
    }
}