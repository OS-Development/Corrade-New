///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Imaging;
using wasSharp;
using Encoder = System.Drawing.Imaging.Encoder;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> download =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    object item =
                        StringOrUUID(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                                    corradeCommandParameters.Message)));
                    if (item == null)
                        throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
                    FieldInfo assetTypeInfo = typeof (AssetType).GetFields(BindingFlags.Public |
                                                                           BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                                            corradeCommandParameters.Message)),
                                    StringComparison.Ordinal));
                    switch (assetTypeInfo != null)
                    {
                        case false:
                            throw new ScriptException(ScriptError.UNKNOWN_ASSET_TYPE);
                    }
                    AssetType assetType = (AssetType) assetTypeInfo.GetValue(null);
                    InventoryItem inventoryItem = null;
                    UUID itemUUID;
                    // If the asset is of an asset type that can only be retrieved locally or the item is a string
                    // then attempt to resolve the item to an inventory item or else the item cannot be found.
                    switch (
                        assetType.Equals(AssetType.LSLText) || assetType.Equals(AssetType.Notecard) ||
                        item is string)
                    {
                        case true:
                            InventoryBase inventoryBaseItem =
                                FindInventory<InventoryBase>(Client.Inventory.Store.RootNode, item).FirstOrDefault();
                            if (inventoryBaseItem == null)
                            {
                                throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                            }
                            inventoryItem = inventoryBaseItem as InventoryItem;
                            if (inventoryItem == null)
                            {
                                throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                            }
                            itemUUID = inventoryItem.AssetUUID;
                            break;
                        default: // otherwise, just set the the item UUID to the item
                            itemUUID = (UUID) item;
                            break;
                    }
                    byte[] assetData = null;
                    switch (!Client.Assets.Cache.HasAsset(itemUUID))
                    {
                        case true:
                            ManualResetEvent RequestAssetEvent = new ManualResetEvent(false);
                            bool succeeded = false;
                            switch (assetType)
                            {
                                case AssetType.Mesh:
                                    Client.Assets.RequestMesh(itemUUID,
                                        delegate(bool completed, AssetMesh asset)
                                        {
                                            if (!asset.AssetID.Equals(itemUUID)) return;
                                            succeeded = completed;
                                            if (succeeded)
                                            {
                                                assetData = asset.MeshData.AsBinary();
                                            }
                                            RequestAssetEvent.Set();
                                        });
                                    if (
                                        !RequestAssetEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                    {
                                        throw new ScriptException(ScriptError.TIMEOUT_TRANSFERRING_ASSET);
                                    }
                                    break;
                                // All of these can only be fetched if they exist locally.
                                case AssetType.LSLText:
                                case AssetType.Notecard:
                                    if (
                                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                                            (int) Configuration.Permissions.Inventory))
                                    {
                                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                                    }
                                    Client.Assets.RequestInventoryAsset(inventoryItem, true,
                                        delegate(AssetDownload transfer, Asset asset)
                                        {
                                            succeeded = transfer.Success;
                                            if (transfer.Success)
                                            {
                                                assetData = asset.AssetData;
                                            }
                                            RequestAssetEvent.Set();
                                        });
                                    if (
                                        !RequestAssetEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                    {
                                        throw new ScriptException(ScriptError.TIMEOUT_TRANSFERRING_ASSET);
                                    }
                                    break;
                                // All images go through RequestImage and can be fetched directly from the asset server.
                                case AssetType.Texture:
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
                                        !RequestAssetEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                    {
                                        throw new ScriptException(ScriptError.TIMEOUT_TRANSFERRING_ASSET);
                                    }
                                    break;
                                // All of these can be fetched directly from the asset server.
                                case AssetType.Landmark:
                                case AssetType.Gesture:
                                case AssetType.Animation: // Animatn
                                case AssetType.Sound: // Ogg Vorbis
                                case AssetType.Clothing:
                                case AssetType.Bodypart:
                                    Client.Assets.RequestAsset(itemUUID, assetType, true,
                                        delegate(AssetDownload transfer, Asset asset)
                                        {
                                            if (!transfer.AssetID.Equals(itemUUID)) return;
                                            succeeded = transfer.Success;
                                            if (transfer.Success)
                                            {
                                                assetData = asset.AssetData;
                                            }
                                            RequestAssetEvent.Set();
                                        });
                                    if (
                                        !RequestAssetEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                    {
                                        throw new ScriptException(ScriptError.TIMEOUT_TRANSFERRING_ASSET);
                                    }
                                    break;
                                default:
                                    throw new ScriptException(ScriptError.UNKNOWN_ASSET_TYPE);
                            }
                            if (!succeeded)
                            {
                                throw new ScriptException(ScriptError.FAILED_TO_DOWNLOAD_ASSET);
                            }
                            Client.Assets.Cache.SaveAssetToCache(itemUUID, assetData);
                            break;
                        default:
                            assetData = Client.Assets.Cache.GetCachedAssetBytes(itemUUID);
                            break;
                    }
                    // If the asset type was a texture, convert it to the desired format.
                    switch (assetType.Equals(AssetType.Texture))
                    {
                        case true:
                            string format =
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FORMAT)),
                                    corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(format))
                            {
                                PropertyInfo formatProperty = typeof (ImageFormat).GetProperties(
                                    BindingFlags.Public |
                                    BindingFlags.Static)
                                    .AsParallel().FirstOrDefault(
                                        o =>
                                            string.Equals(o.Name, format, StringComparison.Ordinal));
                                if (formatProperty == null)
                                {
                                    throw new Exception(
                                        Reflection.GetNameFromEnumValue(
                                            ScriptError.UNKNOWN_IMAGE_FORMAT_REQUESTED));
                                }
                                ManagedImage managedImage;
                                if (!OpenJPEG.DecodeToImage(assetData, out managedImage))
                                {
                                    throw new Exception(
                                        Reflection.GetNameFromEnumValue(
                                            ScriptError.UNABLE_TO_DECODE_ASSET_DATA));
                                }
                                using (MemoryStream imageStream = new MemoryStream())
                                {
                                    try
                                    {
                                        using (Bitmap bitmapImage = managedImage.ExportBitmap())
                                        {
                                            EncoderParameters encoderParameters = new EncoderParameters(1);
                                            encoderParameters.Param[0] =
                                                new EncoderParameter(Encoder.Quality, 100L);
                                            bitmapImage.Save(imageStream,
                                                ImageCodecInfo.GetImageDecoders()
                                                    .AsParallel()
                                                    .FirstOrDefault(
                                                        o =>
                                                            o.FormatID.Equals(
                                                                ((ImageFormat)
                                                                    formatProperty.GetValue(
                                                                        new ImageFormat(Guid.Empty))).Guid)),
                                                encoderParameters);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        throw new Exception(
                                            Reflection.GetNameFromEnumValue(
                                                ScriptError.UNABLE_TO_CONVERT_TO_REQUESTED_FORMAT));
                                    }
                                    assetData = imageStream.ToArray();
                                }
                            }
                            break;
                    }
                    // If no path was specificed, then send the data.
                    string path =
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.PATH)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(path))
                    {
                        result.Add(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                            Convert.ToBase64String(assetData));
                        return;
                    }
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.System))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    // Otherwise, save it to the specified file.
                    using (StreamWriter sw = new StreamWriter(path, false, Encoding.UTF8))
                    {
                        using (BinaryWriter bw = new BinaryWriter(sw.BaseStream, Encoding.UTF8))
                        {
                            bw.Write(assetData);
                            bw.Flush();
                        }
                        sw.Flush();
                    }
                };
        }
    }
}