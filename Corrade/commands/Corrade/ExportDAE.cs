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
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Xml;
using CorradeConfiguration;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Imaging;
using OpenMetaverse.Rendering;
using wasOpenMetaverse;
using wasSharp;
using Encoder = System.Drawing.Imaging.Encoder;
using Helpers = wasOpenMetaverse.Helpers;
using Mesh = wasOpenMetaverse.Mesh;
using Parallel = System.Threading.Tasks.Parallel;
using Path = System.IO.Path;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> exportdae =
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
                    string item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                    {
                        throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
                    }
                    UUID itemUUID;
                    if (UUID.TryParse(item, out itemUUID))
                    {
                        if (
                            !Services.FindPrimitive(Client,
                                itemUUID,
                                range,
                                corradeConfiguration.Range,
                                ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                        {
                            throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                        }
                    }
                    else
                    {
                        if (
                            !Services.FindPrimitive(Client,
                                item,
                                range,
                                corradeConfiguration.Range,
                                ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                        {
                            throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                        }
                    }
                    // if the primitive is not an object (the root) or the primitive
                    // is not an object as an avatar attachment then do not export it.
                    if (!primitive.ParentID.Equals(0) &&
                        !Services.GetAvatars(Client, range, corradeConfiguration.Range,
                            corradeConfiguration.ServicesTimeout,
                            corradeConfiguration.DataTimeout, new Time.DecayingAlarm(corradeConfiguration.DataDecayType))
                            .ToArray()
                            .AsParallel()
                            .Any(o => o.LocalID.Equals(primitive.ParentID)))
                    {
                        throw new ScriptException(ScriptError.ITEM_IS_NOT_AN_OBJECT);
                    }

                    HashSet<Primitive> exportPrimitivesSet = new HashSet<Primitive>();
                    Primitive root = new Primitive(primitive) {Position = Vector3.Zero};
                    exportPrimitivesSet.Add(root);

                    object LockObject = new object();

                    // find all the children that have the object as parent.
                    Parallel.ForEach(
                        Services.GetPrimitives(Client, range, corradeConfiguration.Range,
                            corradeConfiguration.ServicesTimeout,
                            corradeConfiguration.DataTimeout, new Time.DecayingAlarm(corradeConfiguration.DataDecayType)),
                        o =>
                        {
                            if (!o.ParentID.Equals(root.LocalID))
                                return;
                            Primitive child = new Primitive(o);
                            child.Position = root.Position + child.Position*root.Rotation;
                            child.Rotation = root.Rotation*child.Rotation;
                            lock (LockObject)
                            {
                                exportPrimitivesSet.Add(child);
                            }
                        });

                    // update the primitives in the link set
                    if (!Services.UpdatePrimitives(Client, ref exportPrimitivesSet, corradeConfiguration.DataTimeout))
                        throw new ScriptException(ScriptError.COULD_NOT_GET_PRIMITIVE_PROPERTIES);

                    // add all the textures to export
                    HashSet<UUID> exportTexturesSet = new HashSet<UUID>();
                    Parallel.ForEach(exportPrimitivesSet, o =>
                    {
                        Primitive.TextureEntryFace defaultTexture = o.Textures.DefaultTexture;
                        if (defaultTexture != null && !exportTexturesSet.Contains(defaultTexture.TextureID))
                        {
                            lock (LockObject)
                            {
                                exportTexturesSet.Add(defaultTexture.TextureID);
                            }
                        }
                        Parallel.ForEach(o.Textures.FaceTextures, p =>
                        {
                            if (p != null && !exportTexturesSet.Contains(p.TextureID))
                            {
                                lock (LockObject)
                                {
                                    exportTexturesSet.Add(p.TextureID);
                                }
                            }
                        });
                    });

                    // Get the destination format to convert the downloaded textures to.
                    string format =
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
                    Dictionary<string, byte[]> exportTextureSetFiles = new Dictionary<string, byte[]>();
                    Dictionary<UUID, string> exportMeshTextures = new Dictionary<UUID, string>();
                    Parallel.ForEach(exportTexturesSet, o =>
                    {
                        byte[] assetData = null;
                        switch (!Client.Assets.Cache.HasAsset(o))
                        {
                            case true:
                                lock (Locks.ClientInstanceAssetsLock)
                                {
                                    ManualResetEvent RequestAssetEvent = new ManualResetEvent(false);
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
                                Client.Assets.Cache.SaveAssetToCache(o, assetData);
                                break;
                            default:
                                assetData = Client.Assets.Cache.GetCachedAssetBytes(o);
                                break;
                        }
                        switch (formatProperty != null)
                        {
                            case true:
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
                                            EncoderParameters encoderParameters =
                                                new EncoderParameters(1);
                                            encoderParameters.Param[0] =
                                                new EncoderParameter(Encoder.Quality, 100L);
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
                                        throw new Exception(
                                            Reflection.GetNameFromEnumValue(
                                                ScriptError.UNABLE_TO_CONVERT_TO_REQUESTED_FORMAT));
                                    }
                                    lock (LockObject)
                                    {
                                        exportTextureSetFiles.Add(
                                            o + "." + format.ToLower(),
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
                    HashSet<FacetedMesh> exportMeshSet = new HashSet<FacetedMesh>();
                    MeshmerizerR mesher = new MeshmerizerR();
                    Parallel.ForEach(exportPrimitivesSet, o =>
                    {
                        FacetedMesh mesh = null;
                        if (!Mesh.MakeFacetedMesh(Client, o, mesher, ref mesh, corradeConfiguration.ServicesTimeout))
                        {
                            throw new ScriptException(ScriptError.COULD_NOT_MESHMERIZE_OBJECT);
                        }
                        if (mesh == null) return;
                        Parallel.ForEach(mesh.Faces, p =>
                        {
                            Primitive.TextureEntryFace textureEntryFace = p.TextureFace;
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

                    HashSet<char> invalidPathCharacters = new HashSet<char>(Path.GetInvalidPathChars());

                    using (MemoryStream zipMemoryStream = new MemoryStream())
                    {
                        using (
                            ZipArchive zipOutputStream = new ZipArchive(zipMemoryStream, ZipArchiveMode.Create, true)
                            )
                        {
                            ZipArchive zipOutputStreamClosure = zipOutputStream;
                            // add all the textures to the zip file
                            Parallel.ForEach(exportTextureSetFiles, o =>
                            {
                                lock (LockObject)
                                {
                                    ZipArchiveEntry textureEntry =
                                        zipOutputStreamClosure.CreateEntry(
                                            new string(
                                                o.Key.Where(
                                                    p => !invalidPathCharacters.Contains(p)).ToArray()));
                                    using (Stream textureEntryDataStream = textureEntry.Open())
                                    {
                                        using (
                                            BinaryWriter textureEntryDataStreamWriter =
                                                new BinaryWriter(textureEntryDataStream, Encoding.UTF8))
                                        {
                                            textureEntryDataStreamWriter.Write(o.Value);
                                            textureEntryDataStream.Flush();
                                        }
                                    }
                                }
                            });

                            // add the primitives XML data to the zip file
                            ZipArchiveEntry primitiveEntry =
                                zipOutputStreamClosure.CreateEntry(
                                    new string(
                                        (primitive.Properties.Name + ".dae").Where(
                                            p => !invalidPathCharacters.Contains(p))
                                            .ToArray()));
                            using (Stream primitiveEntryDataStream = primitiveEntry.Open())
                            {
                                using (
                                    XmlTextWriter XMLTextWriter = new XmlTextWriter(primitiveEntryDataStream,
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
                        zipMemoryStream.Seek(0, SeekOrigin.Begin);

                        // If no path was specificed, then send the data.
                        string path =
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
                            FileStream fileStream = File.Open(path, FileMode.Create, FileAccess.Write, FileShare.None))
                        {
                            using (StreamWriter streamWriter = new StreamWriter(fileStream, Encoding.UTF8))
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