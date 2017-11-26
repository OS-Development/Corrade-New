///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using CorradeConfigurationSharp;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Imaging;
using OpenMetaverse.Rendering;
using wasOpenMetaverse;
using wasSharp;
using Encoder = System.Drawing.Imaging.Encoder;
using Mesh = wasOpenMetaverse.Mesh;
using Parallel = System.Threading.Tasks.Parallel;
using Path = System.IO.Path;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> exportdae =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                            out range))
                        range = corradeConfiguration.Range;
                    Primitive primitive = null;
                    var item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
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
                                throw new Command.ScriptException(Enumerations.ScriptError.OBJECT_NOT_FOUND);
                            break;

                        default:
                            if (
                                !Services.FindObject(Client,
                                    item,
                                    range,
                                    ref primitive,
                                    corradeConfiguration.DataTimeout))
                                throw new Command.ScriptException(Enumerations.ScriptError.OBJECT_NOT_FOUND);
                            break;
                    }

                    var exportPrimitivesSet = new HashSet<Primitive>();
                    var root = new Primitive(primitive) {Position = Vector3.Zero};
                    exportPrimitivesSet.Add(root);

                    var LockObject = new object();

                    // find all the children that have the object as parent.
                    var scriptError = Enumerations.ScriptError.NONE;
                    Parallel.ForEach(
                        Services.GetPrimitives(Client, range).AsParallel().Where(o => o.ParentID.Equals(root.LocalID)),
                        (o, s) =>
                        {
                            switch (Services.UpdatePrimitive(Client, ref o, corradeConfiguration.DataTimeout))
                            {
                                case true:
                                    o.Position = root.Position + o.Position * root.Rotation;
                                    o.Rotation = root.Rotation * o.Rotation;
                                    lock (LockObject)
                                    {
                                        exportPrimitivesSet.Add(o);
                                    }
                                    break;

                                default:
                                    scriptError = Enumerations.ScriptError.COULD_NOT_GET_PRIMITIVE_PROPERTIES;
                                    s.Break();
                                    return;
                            }
                        });

                    // if the child primitives could not be updated, then throw the error.
                    if (!scriptError.Equals(Enumerations.ScriptError.NONE))
                        throw new Command.ScriptException(scriptError);

                    // add all the textures to export
                    var exportTexturesSet = new HashSet<UUID>();
                    exportPrimitivesSet.AsParallel().ForAll(o =>
                    {
                        var defaultTexture = o.Textures.DefaultTexture;
                        if (defaultTexture != null && !exportTexturesSet.Contains(defaultTexture.TextureID))
                            lock (LockObject)
                            {
                                exportTexturesSet.Add(defaultTexture.TextureID);
                            }
                        o.Textures.FaceTextures.AsParallel()
                            .Where(p => p != null && !exportTexturesSet.Contains(p.TextureID))
                            .ForAll(p =>
                            {
                                lock (LockObject)
                                {
                                    exportTexturesSet.Add(p.TextureID);
                                }
                            });
                    });

                    // Get the destination format to convert the downloaded textures to.
                    var format =
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FORMAT)),
                            corradeCommandParameters.Message));
                    PropertyInfo formatProperty = null;
                    if (!string.IsNullOrEmpty(format))
                    {
                        formatProperty = typeof(ImageFormat).GetProperties(
                                BindingFlags.Public |
                                BindingFlags.Static)
                            .AsParallel().FirstOrDefault(
                                o =>
                                    string.Equals(o.Name, format, StringComparison.Ordinal));
                        if (formatProperty == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_IMAGE_FORMAT_REQUESTED);
                    }

                    // download all the textures.
                    var exportTextureSetFiles = new Dictionary<string, byte[]>();
                    var exportMeshTextures = new Dictionary<UUID, string>();
                    exportTexturesSet.AsParallel().ForAll(o =>
                    {
                        byte[] assetData = null;
                        bool cacheHasAsset;
                        Locks.ClientInstanceAssetsLock.EnterReadLock();
                        cacheHasAsset = Client.Assets.Cache.HasAsset(o);
                        Locks.ClientInstanceAssetsLock.ExitReadLock();
                        switch (!cacheHasAsset)
                        {
                            case true:
                                Locks.ClientInstanceAssetsLock.EnterReadLock();
                                var RequestAssetEvent = new ManualResetEventSlim(false);
                                Client.Assets.RequestImage(o, ImageType.Normal,
                                    delegate(TextureRequestState state, AssetTexture asset)
                                    {
                                        if (!asset.AssetID.Equals(o)) return;
                                        if (!state.Equals(TextureRequestState.Finished)) return;
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
                                if (corradeConfiguration.EnableHorde)
                                    HordeDistributeCacheAsset(o, assetData,
                                        Configuration.HordeDataSynchronizationOption.Add);
                                break;

                            default:
                                Locks.ClientInstanceAssetsLock.EnterReadLock();
                                assetData = Client.Assets.Cache.GetCachedAssetBytes(o);
                                Locks.ClientInstanceAssetsLock.ExitReadLock();
                                break;
                        }
                        switch (formatProperty != null)
                        {
                            case true:
                                ManagedImage managedImage;
                                if (!OpenJPEG.DecodeToImage(assetData, out managedImage))
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.UNABLE_TO_DECODE_ASSET_DATA);
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
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.UNABLE_TO_CONVERT_TO_REQUESTED_FORMAT);
                                    }
                                    lock (LockObject)
                                    {
                                        exportTextureSetFiles.Add(
                                            o + "." + format.ToLowerInvariant(),
                                            imageStream.ToArray());
                                        exportMeshTextures.Add(o,
                                            o.ToString());
                                    }
                                }
                                break;

                            default:
                                format = "j2c";
                                lock (LockObject)
                                {
                                    exportTextureSetFiles.Add(o + "." + "j2c",
                                        assetData);
                                    exportMeshTextures.Add(o,
                                        o.ToString());
                                }
                                break;
                        }
                    });

                    // meshmerize all the primitives
                    var exportMeshSet = new HashSet<FacetedMesh>();
                    var mesher = new MeshmerizerR();
                    exportPrimitivesSet.AsParallel().ForAll(o =>
                    {
                        FacetedMesh mesh = null;
                        if (!Mesh.MakeFacetedMesh(Client, o, mesher, ref mesh, corradeConfiguration.ServicesTimeout))
                            throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_MESHMERIZE_OBJECT);
                        if (mesh == null) return;
                        mesh.Faces.AsParallel().ForAll(p =>
                        {
                            var textureEntryFace = p.TextureFace;
                            if (textureEntryFace == null) return;

                            // Sculpt UV vertically flipped compared to prims. Flip back
                            if (o.Sculpt != null && !o.Sculpt.SculptTexture.Equals(UUID.Zero) &&
                                !o.Sculpt.Type.Equals(SculptType.Mesh))
                            {
                                textureEntryFace = (Primitive.TextureEntryFace) textureEntryFace.Clone();
                                textureEntryFace.RepeatV *= -1;
                            }
                            // Texture transform for this face
                            mesher.TransformTexCoords(p.Vertices, p.Center, textureEntryFace, o.Scale);
                        });
                        lock (LockObject)
                        {
                            exportMeshSet.Add(mesh);
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
                                                o.Key.Where(
                                                    p => !invalidPathCharacters.Contains(p)).ToArray()));
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
                                        (primitive.Properties.Name + ".dae").Where(
                                            p => !invalidPathCharacters.Contains(p))
                                        .ToArray()));
                            using (var primitiveEntryDataStream = primitiveEntry.Open())
                            {
                                using (
                                    var XMLTextWriter = new XmlTextWriter(primitiveEntryDataStream,
                                        Encoding.UTF8))
                                {
                                    XMLTextWriter.Formatting = Formatting.Indented;
                                    XMLTextWriter.WriteProcessingInstruction("xml",
                                        "version=\"1.0\" encoding=\"utf-8\"");
                                    Mesh.GenerateCollada(exportMeshSet, exportMeshTextures, format)
                                        .WriteContentTo(XMLTextWriter);
                                    XMLTextWriter.Flush();
                                }
                            }
                        }

                        // Base64-encode the zip stream and send it.
                        zipMemoryStream.Position = 0;

                        // If no path was specificed, then send the data.
                        var path =
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PATH)),
                                corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(path))
                        {
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                Convert.ToBase64String(zipMemoryStream.ToArray()));
                            return;
                        }
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.System))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        // Otherwise, save it to the specified file.
                        using (
                            var fileStream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None,
                                16384, true))
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