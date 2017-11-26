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
using Corrade.Constants;
using CorradeConfigurationSharp;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> download =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
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
                    // If the asset type was a texture, convert it to the desired format.
                    var format = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FORMAT)),
                        corradeCommandParameters.Message));
                    if (!string.IsNullOrEmpty(format))
                        switch (assetType)
                        {
                            case AssetType.Texture:
                                ManagedImage managedImage;
                                /*
                                 * Use ImageMagick on Windows and the .NET converter otherwise.
                                 */
                                switch (Utils.GetRunningPlatform())
                                {
                                    case Utils.Platform.Windows:
                                        var imageMagickDLL = Assembly.LoadFile(
                                            Path.Combine(Directory.GetCurrentDirectory(), @"libs",
                                                "Magick.NET-Q16-HDRI-AnyCPU.dll"));
                                        var magickFormatType = imageMagickDLL.GetType("ImageMagick.MagickFormat");
                                        var magickFormatValues = Enum.GetValues(magickFormatType);
                                        dynamic magickFormat = Enumerable.Range(0, magickFormatValues.Length)
                                            .AsParallel()
                                            .Select(i => magickFormatValues.GetValue(i))
                                            .FirstOrDefault(i => Enum.GetName(magickFormatType, i)
                                                .Equals(format, StringComparison.Ordinal));
                                        if (magickFormat == null)
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.UNKNOWN_IMAGE_FORMAT_REQUESTED);
                                        if (!OpenJPEG.DecodeToImage(assetData, out managedImage))
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.UNABLE_TO_DECODE_ASSET_DATA);
                                        try
                                        {
                                            using (var bitmapImage = managedImage.ExportBitmap())
                                            {
                                                using (var imageStream = new MemoryStream())
                                                {
                                                    using (dynamic magickImage = imageMagickDLL
                                                        .CreateInstance(
                                                            "ImageMagick.MagickImage",
                                                            false, BindingFlags.CreateInstance, null,
                                                            new[] {bitmapImage}, null, null))
                                                    {
                                                        magickImage.Format = magickFormat;
                                                        magickImage.Write(imageStream);
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
                                        var formatProperty = typeof(ImageFormat).GetProperties(
                                                BindingFlags.Public |
                                                BindingFlags.Static)
                                            .AsParallel().FirstOrDefault(
                                                o =>
                                                    string.Equals(o.Name, format, StringComparison.Ordinal));
                                        if (formatProperty == null)
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.UNKNOWN_IMAGE_FORMAT_REQUESTED);
                                        if (!OpenJPEG.DecodeToImage(assetData, out managedImage))
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.UNABLE_TO_DECODE_ASSET_DATA);
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
                                break;

                            case AssetType.Sound:
                                using (var soundOutputStream = new MemoryStream())
                                {
                                    using (var soundAssetStream = new MemoryStream(assetData))
                                    {
                                        using (var vorbis = new VorbisWaveReader(soundAssetStream))
                                        {
                                            using (var vorbisStream = new MemoryStream())
                                            {
                                                using (var wavWriter = new WaveFileWriter(soundOutputStream,
                                                    vorbis.WaveFormat))
                                                {
                                                    vorbis.CopyTo(vorbisStream);
                                                    var vorbisData = vorbisStream.ToArray();
                                                    wavWriter.Write(vorbisData, 0, vorbisData.Length);
                                                }
                                            }
                                        }
                                    }
                                    switch (format)
                                    {
                                        case "wav":
                                            assetData = soundOutputStream.ToArray();
                                            break;

                                        default:
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.UNKOWN_SOUND_FORMAT_REQUESTED);
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
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    // Otherwise, save it to the specified file.
                    using (
                        var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 16384,
                            true))
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