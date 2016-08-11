///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Imaging;
using OpenMetaverse.StructuredData;
using wasOpenMetaverse;
using wasSharp;
using Encoder = System.Drawing.Imaging.Encoder;
using Helpers = OpenMetaverse.Helpers;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> exportxml =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)),
                            out range))
                    {
                        range = corradeConfiguration.Range;
                    }
                    Primitive primitive = null;
                    var item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                    {
                        throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
                    }
                    UUID itemUUID;
                    switch (UUID.TryParse(item, out itemUUID))
                    {
                        case true:
                            if (
                                !Services.FindObject(Client,
                                    itemUUID,
                                    range,
                                    ref primitive,
                                    corradeConfiguration.DataTimeout))
                            {
                                throw new ScriptException(ScriptError.OBJECT_NOT_FOUND);
                            }
                            break;
                        default:
                            if (
                                !Services.FindObject(Client,
                                    item,
                                    range,
                                    ref primitive,
                                    corradeConfiguration.DataTimeout))
                            {
                                throw new ScriptException(ScriptError.OBJECT_NOT_FOUND);
                            }
                            break;
                    }

                    var exportPrimitivesSet = new HashSet<Primitive>();
                    var root = new Primitive(primitive) {Position = Vector3.Zero};
                    exportPrimitivesSet.Add(root);

                    var LockObject = new object();

                    // find all the children that have the object as parent.
                    Services.GetPrimitives(Client, range)
                        .ToArray()
                        .AsParallel()
                        .Where(o => o.ParentID.Equals(root.LocalID))
                        .ForAll(
                            o =>
                            {
                                var child = new Primitive(o);
                                child.Position = root.Position + child.Position*root.Rotation;
                                child.Rotation = root.Rotation*child.Rotation;
                                lock (LockObject)
                                {
                                    exportPrimitivesSet.Add(child);
                                }
                            });

                    // add all the textures to export
                    var exportTexturesSet = new HashSet<UUID>();
                    exportPrimitivesSet.AsParallel().ForAll(o =>
                    {
                        if (!o.Textures.DefaultTexture.TextureID.Equals(Primitive.TextureEntry.WHITE_TEXTURE) &&
                            !exportTexturesSet.Contains(o.Textures.DefaultTexture.TextureID))
                        {
                            lock (LockObject)
                            {
                                exportTexturesSet.Add(new UUID(o.Textures.DefaultTexture.TextureID));
                            }
                        }
                        o.Textures.FaceTextures.AsParallel()
                            .Where(p => p != null && !p.TextureID.Equals(Primitive.TextureEntry.WHITE_TEXTURE) &&
                                        !exportTexturesSet.Contains(p.TextureID)).ForAll(p =>
                                        {
                                            lock (LockObject)
                                            {
                                                exportTexturesSet.Add(new UUID(p.TextureID));
                                            }
                                        });
                        if (o.Sculpt != null && !o.Sculpt.SculptTexture.Equals(UUID.Zero) &&
                            !exportTexturesSet.Contains(o.Sculpt.SculptTexture))
                        {
                            lock (LockObject)
                            {
                                exportTexturesSet.Add(new UUID(o.Sculpt.SculptTexture));
                            }
                        }
                    });

                    // Get the destination format to convert the downloaded textures to.
                    var format =
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FORMAT)),
                            corradeCommandParameters.Message));
                    PropertyInfo formatProperty = null;
                    if (!string.IsNullOrEmpty(format))
                    {
                        formatProperty = typeof (ImageFormat).GetProperties(
                            BindingFlags.Public |
                            BindingFlags.Static)
                            .AsParallel().FirstOrDefault(
                                o =>
                                    string.Equals(o.Name, format, StringComparison.Ordinal));
                        if (formatProperty == null)
                        {
                            throw new ScriptException(ScriptError.UNKNOWN_IMAGE_FORMAT_REQUESTED);
                        }
                    }

                    // download all the textures.
                    var exportTextureSetFiles = new Dictionary<string, byte[]>();
                    exportTexturesSet.AsParallel().ForAll(o =>
                    {
                        byte[] assetData = null;
                        bool cacheHasAsset;
                        lock (Locks.ClientInstanceAssetsLock)
                        {
                            cacheHasAsset = Client.Assets.Cache.HasAsset(o);
                        }
                        switch (!cacheHasAsset)
                        {
                            case true:
                                lock (Locks.ClientInstanceAssetsLock)
                                {
                                    var RequestAssetEvent = new ManualResetEvent(false);
                                    Client.Assets.RequestImage(o, ImageType.Normal,
                                        delegate(TextureRequestState state, AssetTexture asset)
                                        {
                                            if (!asset.AssetID.Equals(o)) return;
                                            if (!state.Equals(TextureRequestState.Finished)) return;
                                            assetData = asset.AssetData;
                                            RequestAssetEvent.Set();
                                        });
                                    if (
                                        !RequestAssetEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                    {
                                        throw new ScriptException(ScriptError.TIMEOUT_TRANSFERRING_ASSET);
                                    }
                                }
                                lock (Locks.ClientInstanceAssetsLock)
                                {
                                    Client.Assets.Cache.SaveAssetToCache(o, assetData);
                                }
                                PushBinaryAssetCache(o, assetData);
                                break;
                            default:
                                lock (Locks.ClientInstanceAssetsLock)
                                {
                                    assetData = Client.Assets.Cache.GetCachedAssetBytes(o);
                                }
                                break;
                        }
                        switch (formatProperty != null)
                        {
                            case true:
                                ManagedImage managedImage;
                                if (!OpenJPEG.DecodeToImage(assetData, out managedImage))
                                {
                                    throw new ScriptException(ScriptError.UNABLE_TO_DECODE_ASSET_DATA);
                                }
                                using (var imageStream = new MemoryStream())
                                {
                                    try
                                    {
                                        using (var bitmapImage = managedImage.ExportBitmap())
                                        {
                                            var encoderParameters =
                                                new EncoderParameters(1)
                                                {
                                                    Param = {[0] = new EncoderParameter(Encoder.Quality, 100L)}
                                                };
                                            bitmapImage.Save(imageStream,
                                                ImageCodecInfo.GetImageDecoders()
                                                    .AsParallel()
                                                    .FirstOrDefault(
                                                        p =>
                                                            p.FormatID.Equals(
                                                                ((ImageFormat)
                                                                    formatProperty.GetValue(
                                                                        new ImageFormat(Guid.Empty)))
                                                                    .Guid)),
                                                encoderParameters);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        throw new ScriptException(ScriptError.UNABLE_TO_CONVERT_TO_REQUESTED_FORMAT);
                                    }
                                    lock (LockObject)
                                    {
                                        exportTextureSetFiles.Add(
                                            o + "." + format.ToLower(),
                                            imageStream.ToArray());
                                    }
                                }
                                break;
                            default:
                                format = "j2c";
                                lock (LockObject)
                                {
                                    exportTextureSetFiles.Add(o + "." + "j2c",
                                        assetData);
                                }
                                break;
                        }
                    });

                    var invalidPathCharacters = new HashSet<char>(Path.GetInvalidPathChars());

                    using (var zipMemoryStream = new MemoryStream())
                    {
                        using (
                            var zipOutputStream = new ZipArchive(zipMemoryStream, ZipArchiveMode.Create, true)
                            )
                        {
                            var zipOutputStreamClosure = zipOutputStream;
                            // add all the textures to the zip file
                            exportTextureSetFiles.AsParallel().ForAll(o =>
                            {
                                lock (LockObject)
                                {
                                    var textureEntry =
                                        zipOutputStreamClosure.CreateEntry(
                                            new string(
                                                o.Key.Where(p => !invalidPathCharacters.Contains(p)).ToArray()));
                                    using (var textureEntryDataStream = textureEntry.Open())
                                    {
                                        using (
                                            var textureEntryDataStreamWriter =
                                                new BinaryWriter(textureEntryDataStream, Encoding.UTF8))
                                        {
                                            textureEntryDataStreamWriter.Write(o.Value);
                                            textureEntryDataStream.Flush();
                                        }
                                    }
                                }
                            });

                            // add the primitives XML data to the zip file
                            var primitiveEntry =
                                zipOutputStreamClosure.CreateEntry(
                                    new string(
                                        (primitive.Properties.Name + ".xml").Where(
                                            p => !invalidPathCharacters.Contains(p))
                                            .ToArray()));
                            using (var primitiveEntryDataStream = primitiveEntry.Open())
                            {
                                using (
                                    var primitiveEntryDataStreamWriter =
                                        new StreamWriter(primitiveEntryDataStream, Encoding.UTF8))
                                {
                                    primitiveEntryDataStreamWriter.Write(
                                        OSDParser.SerializeLLSDXmlString(
                                            Helpers.PrimListToOSD(exportPrimitivesSet.ToList())));
                                    primitiveEntryDataStreamWriter.Flush();
                                }
                            }
                        }

                        // Base64-encode the zip stream and send it.
                        zipMemoryStream.Seek(0, SeekOrigin.Begin);

                        // If no path was specificed, then send the data.
                        var path =
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.PATH)),
                                corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(path))
                        {
                            result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                                Convert.ToBase64String(zipMemoryStream.ToArray()));
                            return;
                        }
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.System))
                        {
                            throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                        }
                        // Otherwise, save it to the specified file.
                        using (
                            var fileStream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            using (var streamWriter = new StreamWriter(fileStream, Encoding.UTF8))
                            {
                                zipMemoryStream.WriteTo(streamWriter.BaseStream);
                                zipMemoryStream.Flush();
                            }
                        }
                    }
                };
        }
    }
}