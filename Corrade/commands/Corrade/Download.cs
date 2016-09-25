///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using CorradeConfiguration;
using ImageMagick;
using NAudio.Lame;
using NAudio.Vorbis;
using NAudio.Wave;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Imaging;
using wasOpenMetaverse;
using wasSharp;
using Encoder = System.Drawing.Imaging.Encoder;
using Inventory = wasOpenMetaverse.Inventory;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Command.CorradeCommandParameters, Dictionary<string, string>> download =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var item = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                    var assetTypeInfo = typeof (AssetType).GetFields(BindingFlags.Public |
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
                    InventoryItem inventoryItem = null;
                    UUID itemUUID;
                    // If the asset is of an asset type that can only be retrieved locally or the item is a string
                    // then attempt to resolve the item to an inventory item or else the item cannot be found.
                    if (!UUID.TryParse(item, out itemUUID))
                    {
                        inventoryItem =
                            Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode, item,
                                corradeConfiguration.ServicesTimeout)
                                .FirstOrDefault() as InventoryItem;
                        if (inventoryItem == null)
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                        }
                        itemUUID = inventoryItem.AssetUUID;
                    }
                    byte[] assetData = null;
                    bool cacheHasAsset;
                    lock (Locks.ClientInstanceAssetsLock)
                    {
                        cacheHasAsset = Client.Assets.Cache.HasAsset(itemUUID);
                    }
                    switch (!cacheHasAsset)
                    {
                        case true:
                            var RequestAssetEvent = new ManualResetEvent(false);
                            var succeeded = false;
                            switch (assetType)
                            {
                                case AssetType.Mesh:
                                    lock (Locks.ClientInstanceAssetsLock)
                                    {
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
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.TIMEOUT_TRANSFERRING_ASSET);
                                        }
                                    }
                                    break;
                                // All of these can only be fetched if they exist locally.
                                case AssetType.LSLText:
                                case AssetType.Notecard:
                                    if (
                                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                            (int) Configuration.Permissions.Inventory))
                                    {
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                                    }
                                    if (inventoryItem == null)
                                    {
                                        inventoryItem =
                                            Inventory.FindInventory<InventoryBase>(Client,
                                                Client.Inventory.Store.RootNode, itemUUID,
                                                corradeConfiguration.ServicesTimeout).FirstOrDefault() as InventoryItem;
                                        if (inventoryItem == null)
                                        {
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                                        }
                                    }
                                    lock (Locks.ClientInstanceAssetsLock)
                                    {
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
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.TIMEOUT_TRANSFERRING_ASSET);
                                        }
                                    }
                                    break;
                                // All images go through RequestImage and can be fetched directly from the asset server.
                                case AssetType.Texture:
                                    lock (Locks.ClientInstanceAssetsLock)
                                    {
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
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.TIMEOUT_TRANSFERRING_ASSET);
                                        }
                                    }
                                    break;
                                // All of these can be fetched directly from the asset server.
                                case AssetType.Landmark:
                                case AssetType.Gesture:
                                case AssetType.Animation: // Animatn
                                case AssetType.Sound: // Ogg Vorbis
                                case AssetType.Clothing:
                                case AssetType.Bodypart:
                                    lock (Locks.ClientInstanceAssetsLock)
                                    {
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
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.TIMEOUT_TRANSFERRING_ASSET);
                                        }
                                    }
                                    break;
                                default:
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ASSET_TYPE);
                            }
                            if (!succeeded)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.FAILED_TO_DOWNLOAD_ASSET);
                            }
                            lock (Locks.ClientInstanceAssetsLock)
                            {
                                Client.Assets.Cache.SaveAssetToCache(itemUUID, assetData);
                            }
                            if (corradeConfiguration.EnableHorde)
                                HordeDistributeCacheAsset(itemUUID, assetData,
                                    Configuration.HordeDataSynchronizationOption.Add);
                            break;
                        default:
                            lock (Locks.ClientInstanceAssetsLock)
                            {
                                assetData = Client.Assets.Cache.GetCachedAssetBytes(itemUUID);
                            }
                            break;
                    }
                    // If the asset type was a texture, convert it to the desired format.
                    switch (assetType.Equals(AssetType.Texture))
                    {
                        case true:
                            var format =
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FORMAT)),
                                    corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(format))
                            {
                                ManagedImage managedImage;
                                /*
                                 * Use ImageMagick on Windows and the .NET converter otherwise.
                                 */
                                switch (Environment.OSVersion.Platform)
                                {
                                    case PlatformID.Win32NT:
                                        var magickFormat = Enum.GetValues(typeof (MagickFormat))
                                            .Cast<MagickFormat>()
                                            .Select(i => new {i, name = Enum.GetName(typeof (MagickFormat), i)})
                                            .Where(
                                                @t =>
                                                    t.name != null && t.name.Equals(format, StringComparison.Ordinal))
                                            .Select(@t => t.i).FirstOrDefault();
                                        if (magickFormat.Equals(MagickFormat.Unknown))
                                        {
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.UNKNOWN_IMAGE_FORMAT_REQUESTED);
                                        }
                                        if (!OpenJPEG.DecodeToImage(assetData, out managedImage))
                                        {
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.UNABLE_TO_DECODE_ASSET_DATA);
                                        }
                                        try
                                        {
                                            using (var bitmapImage = managedImage.ExportBitmap())
                                            {
                                                using (var imageStream = new MemoryStream())
                                                {
                                                    using (var image = new MagickImage(bitmapImage))
                                                    {
                                                        image.Format = magickFormat;
                                                        image.Write(imageStream);
                                                    }
                                                    assetData = imageStream.ToArray();
                                                }
                                            }
                                        }
                                        catch (Exception)
                                        {
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.UNABLE_TO_CONVERT_TO_REQUESTED_FORMAT);
                                        }
                                        break;
                                    default:
                                        var formatProperty = typeof (ImageFormat).GetProperties(
                                            BindingFlags.Public |
                                            BindingFlags.Static)
                                            .AsParallel().FirstOrDefault(
                                                o =>
                                                    Strings.Equals(o.Name, format, StringComparison.Ordinal));
                                        if (formatProperty == null)
                                        {
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.UNKNOWN_IMAGE_FORMAT_REQUESTED);
                                        }
                                        if (!OpenJPEG.DecodeToImage(assetData, out managedImage))
                                        {
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.UNABLE_TO_DECODE_ASSET_DATA);
                                        }
                                        using (var imageStream = new MemoryStream())
                                        {
                                            try
                                            {
                                                using (var bitmapImage = managedImage.ExportBitmap())
                                                {
                                                    var encoderParameters = new EncoderParameters(1);
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
                                                throw new Command.ScriptException(
                                                    Enumerations.ScriptError.UNABLE_TO_CONVERT_TO_REQUESTED_FORMAT);
                                            }
                                            assetData = imageStream.ToArray();
                                        }
                                        break;
                                }
                            }
                            break;
                    }
                    // If the asset type was a sound, convert it to the desired format.
                    switch (assetType.Equals(AssetType.Sound))
                    {
                        case true:
                            var format =
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FORMAT)),
                                    corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(format))
                            {
                                using (var soundOutputStream = new MemoryStream())
                                {
                                    using (var soundAssetStream = new MemoryStream(assetData))
                                    {
                                        using (var vorbis = new VorbisWaveReader(soundAssetStream))
                                        {
                                            using (var vorbisStream = new MemoryStream())
                                            {
                                                switch (format)
                                                {
                                                    case "mp3":
                                                        using (
                                                            var mp3Writer = new LameMP3FileWriter(soundOutputStream,
                                                                vorbis.WaveFormat, LAMEPreset.ABR_256))
                                                        {
                                                            vorbis.CopyTo(vorbisStream);
                                                            var vorbisData = vorbisStream.ToArray();
                                                            mp3Writer.Write(vorbisData, 0, vorbisData.Length);
                                                        }
                                                        break;
                                                    case "wav":
                                                        using (
                                                            var wavWriter = new WaveFileWriter(soundOutputStream,
                                                                vorbis.WaveFormat))
                                                        {
                                                            vorbis.CopyTo(vorbisStream);
                                                            var vorbisData = vorbisStream.ToArray();
                                                            wavWriter.Write(vorbisData, 0, vorbisData.Length);
                                                        }
                                                        break;
                                                    default:
                                                        throw new Command.ScriptException(
                                                            Enumerations.ScriptError.UNKOWN_SOUND_FORMAT_REQUESTED);
                                                }
                                            }
                                        }
                                    }
                                    assetData = soundOutputStream.ToArray();
                                }
                            }
                            break;
                    }
                    // If no path was specificed, then send the data.
                    var path =
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PATH)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(path))
                    {
                        result.Add(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                            Convert.ToBase64String(assetData));
                        return;
                    }
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.System))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    // Otherwise, save it to the specified file.
                    using (var fileStream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None))
                    {
                        using (var binaryWriter = new BinaryWriter(fileStream, Encoding.UTF8))
                        {
                            binaryWriter.Write(assetData);
                            binaryWriter.Flush();
                        }
                    }
                };
        }
    }
}