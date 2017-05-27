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
using System.Linq;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getinventoryassetdata
                =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int)Configuration.Permissions.Inventory))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }

                    var item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    InventoryItem inventoryItem = null;
                    if (!string.IsNullOrEmpty(item))
                    {
                        UUID itemUUID;
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

                        if (inventoryItem == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                    }

                    byte[] assetData = null;
                    Locks.ClientInstanceAssetsLock.EnterReadLock();
                    var cacheHasAsset = Client.Assets.Cache.HasAsset(inventoryItem.AssetUUID);
                    Locks.ClientInstanceAssetsLock.ExitReadLock();
                    bool succeeded = false;
                    switch (!cacheHasAsset)
                    {
                        case true:
                            var RequestAssetEvent = new ManualResetEvent(false);
                            switch (inventoryItem.AssetType)
                            {
                                case AssetType.Mesh:
                                    Locks.ClientInstanceAssetsLock.EnterReadLock();
                                    Client.Assets.RequestMesh(inventoryItem.AssetUUID,
                                            delegate (bool completed, AssetMesh asset)
                                            {
                                                if (!asset.AssetID.Equals(inventoryItem.AssetUUID)) return;
                                                succeeded = completed;
                                                if (succeeded)
                                                {
                                                    assetData = asset.MeshData.AsBinary();
                                                }
                                                RequestAssetEvent.Set();
                                            });
                                    if (
                                        !RequestAssetEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, true))
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
                                            (int)Configuration.Permissions.Inventory))
                                    {
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                                    }
                                    if (inventoryItem == null)
                                    {
                                        Locks.ClientInstanceInventoryLock.EnterReadLock();
                                        if (Client.Inventory.Store.Contains(inventoryItem.AssetUUID))
                                        {
                                            inventoryItem = Client.Inventory.Store[inventoryItem.UUID] as InventoryItem;
                                        }
                                        Locks.ClientInstanceInventoryLock.ExitReadLock();
                                        if (inventoryItem == null)
                                        {
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                                        }
                                    }
                                    Locks.ClientInstanceAssetsLock.EnterReadLock();
                                    Client.Assets.RequestInventoryAsset(inventoryItem, true,
                                            delegate (AssetDownload transfer, Asset asset)
                                            {
                                                succeeded = transfer.Success;
                                                if (transfer.Success)
                                                {
                                                    assetData = asset.AssetData;
                                                }
                                                RequestAssetEvent.Set();
                                            });
                                    if (
                                        !RequestAssetEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, true))
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
                                    Client.Assets.RequestImage(inventoryItem.AssetUUID, ImageType.Normal,
                                            delegate (TextureRequestState state, AssetTexture asset)
                                            {
                                                if (!asset.AssetID.Equals(inventoryItem.AssetUUID)) return;
                                                if (!state.Equals(TextureRequestState.Finished)) return;
                                                assetData = asset.AssetData;
                                                succeeded = true;
                                                RequestAssetEvent.Set();
                                            });
                                    if (
                                        !RequestAssetEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, true))
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
                                    Client.Assets.RequestAsset(inventoryItem.AssetUUID, inventoryItem.AssetType, true,
                                            delegate (AssetDownload transfer, Asset asset)
                                            {
                                                if (!transfer.AssetID.Equals(inventoryItem.AssetUUID)) return;
                                                succeeded = transfer.Success;
                                                if (transfer.Success)
                                                {
                                                    assetData = asset.AssetData;
                                                }
                                                RequestAssetEvent.Set();
                                            });
                                    if (!RequestAssetEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, true))
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
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.FAILED_TO_DOWNLOAD_ASSET);
                            }
                            Locks.ClientInstanceAssetsLock.EnterWriteLock();
                            Client.Assets.Cache.SaveAssetToCache(inventoryItem.AssetUUID, assetData);
                            Locks.ClientInstanceAssetsLock.ExitWriteLock();
                            if (corradeConfiguration.EnableHorde)
                                HordeDistributeCacheAsset(inventoryItem.AssetUUID, assetData,
                                    Configuration.HordeDataSynchronizationOption.Add);
                            break;

                        default:
                            Locks.ClientInstanceAssetsLock.EnterReadLock();
                            assetData = Client.Assets.Cache.GetCachedAssetBytes(inventoryItem.AssetUUID);
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
                    {
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                    }
                };
        }
    }
}
