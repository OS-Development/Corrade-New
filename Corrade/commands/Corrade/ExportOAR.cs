///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;
using System.Xml;
using CorradeConfigurationSharp;
using OpenMetaverse;
using OpenMetaverse.Assets;
using wasOpenMetaverse;
using wasSharp;
using Parallel = System.Threading.Tasks.Parallel;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> exportoar =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                    //var assetPrimitive = new AssetPrim(root, children);
                    Func<HashSet<AssetPrim>, Dictionary<string, byte[]>> getObjectAssetData = o =>
                    {
                        var data = new Dictionary<string, byte[]>();
                        var scriptError = Enumerations.ScriptError.NONE;
                        var DataLock = new object();
                        Parallel.ForEach(o, (assetPrimitive, assetPrimitivesState) =>
                        {
                            Parallel.ForEach(
                                new List<PrimObject> {assetPrimitive.Parent}.Concat(assetPrimitive.Children),
                                (primObject, primObjectState) =>
                                {
                                    // Collect primitive textures.
                                    var textures = new HashSet<UUID>();
                                    if (primObject.Textures != null)
                                    {
                                        // Add all of the textures on this prim to the save list
                                        if (primObject.Textures.DefaultTexture != null &&
                                            !textures.Contains(primObject.Textures.DefaultTexture.TextureID))
                                            textures.Add(primObject.Textures.DefaultTexture.TextureID);

                                        if (primObject.Textures.FaceTextures != null)
                                            for (var i = 0; i < primObject.Textures.FaceTextures.Length; i++)
                                            {
                                                var face = primObject.Textures.FaceTextures[i];
                                                if (face != null && !textures.Contains(face.TextureID))
                                                    textures.Add(face.TextureID);
                                            }
                                        if (primObject.Sculpt != null && primObject.Sculpt.Texture != UUID.Zero &&
                                            !textures.Contains(primObject.Sculpt.Texture))
                                            textures.Add(primObject.Sculpt.Texture);
                                    }

                                    // Download textures.
                                    textures.AsParallel().ForAll(texture =>
                                    {
                                        byte[] textureBytes = null;
                                        if (Services.DownloadTexture(Client, texture, out textureBytes,
                                            corradeConfiguration.DataTimeout))
                                        {
                                            if (
                                                !ArchiveConstants.ASSET_TYPE_TO_EXTENSION.ContainsKey(
                                                    AssetType.Texture))
                                                return;

                                            var textureFile = texture.ToString() +
                                                              ArchiveConstants.ASSET_TYPE_TO_EXTENSION[AssetType.Texture
                                                              ];
                                            lock (DataLock)
                                            {
                                                if (!data.ContainsKey(textureFile))
                                                    data.Add(textureFile, textureBytes);
                                            }
                                        }
                                    });

                                    var taskInventoryItems =
                                        Client.Inventory.GetTaskInventory(primObject.ID, primObject.LocalID,
                                                (int) corradeConfiguration.ServicesTimeout)
                                            .Select(q => q as InventoryItem)
                                            .Where(q => q != null)
                                            .ToList();

                                    if (taskInventoryItems.Any())
                                    {
                                        // Construct the primitive inventory block.
                                        primObject.Inventory = new PrimObject.InventoryBlock();
                                        primObject.Inventory.Items =
                                            new PrimObject.InventoryBlock.ItemBlock[taskInventoryItems.Count];
                                        taskInventoryItems.Select(PrimObject.InventoryBlock.ItemBlock.FromInventoryBase)
                                            .ToList()
                                            .CopyTo(primObject.Inventory.Items);

                                        // Download inventory assets.
                                        foreach (var inventoryItem in taskInventoryItems)
                                        {
                                            var RequestAssetEvent = new ManualResetEventSlim(false);
                                            byte[] assetBytes = null;
                                            var succeeded = false;
                                            switch (inventoryItem.AssetType)
                                            {
                                                case AssetType.Mesh:
                                                    Locks.ClientInstanceAssetsLock.EnterReadLock();
                                                    Client.Assets.RequestMesh(inventoryItem.AssetUUID,
                                                        delegate(bool completed, AssetMesh asset)
                                                        {
                                                            if (!asset.AssetID.Equals(inventoryItem.AssetUUID))
                                                                return;
                                                            succeeded = completed;
                                                            if (succeeded)
                                                                assetBytes = asset.MeshData.AsBinary();
                                                            RequestAssetEvent.Set();
                                                        });
                                                    Locks.ClientInstanceAssetsLock.ExitReadLock();
                                                    break;

                                                case AssetType.Texture:
                                                    succeeded = Services.DownloadTexture(Client,
                                                        inventoryItem.AssetUUID,
                                                        out assetBytes, corradeConfiguration.DataTimeout);
                                                    RequestAssetEvent.Set();
                                                    break;

                                                case AssetType.LSLText:
                                                case AssetType.Notecard:
                                                    Locks.ClientInstanceAssetsLock.EnterReadLock();
                                                    Client.Assets.RequestInventoryAsset(inventoryItem.AssetUUID,
                                                        inventoryItem.UUID, primObject.ID, inventoryItem.OwnerID,
                                                        inventoryItem.AssetType, true,
                                                        delegate(AssetDownload transfer, Asset asset)
                                                        {
                                                            succeeded = transfer.Success;
                                                            if (transfer.Success)
                                                                assetBytes = asset.AssetData;
                                                            RequestAssetEvent.Set();
                                                        });
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
                                                    Client.Assets.RequestAsset(inventoryItem.AssetUUID,
                                                        inventoryItem.AssetType, true,
                                                        delegate(AssetDownload transfer, Asset asset)
                                                        {
                                                            if (!transfer.AssetID.Equals(inventoryItem.AssetUUID))
                                                                return;
                                                            succeeded = transfer.Success;
                                                            if (transfer.Success)
                                                                assetBytes = asset.AssetData;
                                                            RequestAssetEvent.Set();
                                                        });
                                                    Locks.ClientInstanceAssetsLock.ExitReadLock();
                                                    break;
                                            }

                                            if (
                                                !RequestAssetEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                                            {
                                                scriptError = Enumerations.ScriptError.TIMEOUT_TRANSFERRING_ASSET;
                                                primObjectState.Break();
                                                return;
                                            }

                                            // Get any asset-specific textures.
                                            textures.Clear();
                                            switch (inventoryItem.AssetType)
                                            {
                                                case AssetType.Clothing:
                                                    var clothing =
                                                        new AssetClothing(inventoryItem.AssetUUID, assetBytes);
                                                    if (clothing.Decode())
                                                        if (clothing.Textures != null)
                                                            textures.UnionWith(clothing.Textures.Values);
                                                    break;

                                                case AssetType.Bodypart:
                                                    var bodypart =
                                                        new AssetBodypart(inventoryItem.AssetUUID, assetBytes);
                                                    if (bodypart.Decode())
                                                        if (bodypart.Textures != null)
                                                            textures.UnionWith(bodypart.Textures.Values);
                                                    break;
                                            }

                                            // Download textures.
                                            textures.AsParallel().ForAll(texture =>
                                            {
                                                byte[] textureBytes = null;
                                                if (Services.DownloadTexture(Client, texture, out textureBytes,
                                                    corradeConfiguration.DataTimeout))
                                                {
                                                    if (
                                                        !ArchiveConstants.ASSET_TYPE_TO_EXTENSION.ContainsKey(
                                                            AssetType.Texture))
                                                        return;

                                                    var textureFile = texture.ToString() +
                                                                      ArchiveConstants.ASSET_TYPE_TO_EXTENSION[
                                                                          AssetType.Texture];
                                                    lock (DataLock)
                                                    {
                                                        if (!data.ContainsKey(textureFile))
                                                            data.Add(textureFile, textureBytes);
                                                    }
                                                }
                                            });

                                            if (succeeded)
                                            {
                                                var extension = string.Empty;
                                                if (
                                                    ArchiveConstants.ASSET_TYPE_TO_EXTENSION.ContainsKey(
                                                        inventoryItem.AssetType))
                                                    extension =
                                                        ArchiveConstants.ASSET_TYPE_TO_EXTENSION[inventoryItem.AssetType
                                                        ];

                                                data.Add(inventoryItem.UUID.ToString() + extension, assetBytes);
                                            }
                                        }
                                    }
                                });

                            // If an error was encountered processing assets.
                            if (!scriptError.Equals(Enumerations.ScriptError.NONE))
                                assetPrimitivesState.Break();
                        });

                        // If an error was encountered processing objects.
                        if (!scriptError.Equals(Enumerations.ScriptError.NONE))
                            throw new Command.ScriptException(scriptError);

                        return data;
                    };

                    Func<HashSet<AssetPrim>, Dictionary<string, byte[]>> getObjectParamsData = o =>
                    {
                        var data = new Dictionary<string, byte[]>();
                        var scriptError = Enumerations.ScriptError.NONE;
                        var DataLock = new object();
                        Parallel.ForEach(o, (assetPrimitive, assetPrimitivesState) =>
                        {
                            using (var objectMemoryStream = new MemoryStream())
                            {
                                try
                                {
                                    using (var objectMemoryStreamWriter = new StreamWriter(objectMemoryStream))
                                    {
                                        using (var writer = new XmlTextWriter(objectMemoryStreamWriter))
                                        {
                                            writer.Formatting =
                                                wasOpenMetaverse.Constants.OAR.OBJECT_FILE_XML_FORMATTING;
                                            writer.Indentation =
                                                wasOpenMetaverse.Constants.OAR.OBJECT_FILE_XML_INDENTATION;
                                            writer.IndentChar =
                                                wasOpenMetaverse.Constants.OAR.OBJECT_FILE_XML_INDENT_CHAR;
                                            OarFile.SOGToXml2(writer, assetPrimitive);
                                        }
                                    }
                                    data.Add(assetPrimitive.Parent.ID.ToString(), objectMemoryStream.ToArray());
                                }
                                catch (Exception ex)
                                {
                                    result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), ex.Message);
                                    scriptError = Enumerations.ScriptError.UNABLE_TO_SERIALIZE_PRIMITIVE;
                                    assetPrimitivesState.Break();
                                }
                            }
                        });

                        // If an error was encountered processing objects.
                        if (!scriptError.Equals(Enumerations.ScriptError.NONE))
                            throw new Command.ScriptException(scriptError);

                        return data;
                    };

                    var assetsData = new Dictionary<string, byte[]>();
                    var objectData = new Dictionary<string, byte[]>();

                    var LockObject = new object();
                    var assetPrimitives = new HashSet<AssetPrim>();
                    var entity = Reflection.GetEnumValueFromName<Enumerations.Entity>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message))
                    );
                    switch (entity)
                    {
                        case Enumerations.Entity.OBJECT:
                            float range;
                            if (!float.TryParse(
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                    corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                out range))
                                range = corradeConfiguration.Range;
                            var item = wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                                corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(item))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                            Primitive primitive = null;
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
                            // find the simulator of the object.
                            var primitiveSimulator = Client.Network.Simulators.AsParallel().FirstOrDefault(
                                o => o.Handle.Equals(primitive.RegionHandle));
                            if (primitiveSimulator == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.OBJECT_NOT_FOUND);

                            var children = new List<PrimObject>();
                            var root = PrimObject.FromPrimitive(new Primitive(primitive) {Position = Vector3.Zero});

                            // find all the children that have the object as parent.
                            var scriptError = Enumerations.ScriptError.NONE;
                            Parallel.ForEach(
                                Services.GetPrimitives(Client, range)
                                    .AsParallel()
                                    .Where(o => o.ParentID.Equals(root.LocalID)), (o, s) =>
                                {
                                    switch (
                                        Services.UpdatePrimitive(Client, ref o, corradeConfiguration.DataTimeout))
                                    {
                                        case true:
                                            o.Position = root.Position + o.Position * root.Rotation;
                                            o.Rotation = root.Rotation * o.Rotation;
                                            lock (LockObject)
                                            {
                                                children.Add(PrimObject.FromPrimitive(o));
                                            }
                                            break;

                                        default:
                                            scriptError =
                                                Enumerations.ScriptError.COULD_NOT_GET_PRIMITIVE_PROPERTIES;
                                            s.Break();
                                            return;
                                    }
                                });

                            // if the child primitives could not be updated, then throw the error.
                            if (!scriptError.Equals(Enumerations.ScriptError.NONE))
                                throw new Command.ScriptException(scriptError);

                            assetPrimitives.Add(new AssetPrim(root, children));
                            assetsData = getObjectAssetData(assetPrimitives);
                            objectData = getObjectParamsData(assetPrimitives);
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }

                    // Build the OAR
                    using (var tarMemoryStream = new MemoryStream())
                    {
                        using (var gzipStream = new GZipStream(tarMemoryStream, CompressionMode.Compress, true))
                        {
                            var tarArchiveWriter = new TarArchiveWriter(gzipStream);

                            // Create the archive.xml file.
                            tarArchiveWriter.WriteFile(wasOpenMetaverse.Constants.OAR.ARCHIVE_FILE_NAME,
                                wasOpenMetaverse.Constants.OAR.ARCHIVE_FILE_CONTENT);

                            // Archive the object parameters
                            foreach (var primitiveObject in objectData)
                                tarArchiveWriter.WriteFile(
                                    ArchiveConstants.OBJECTS_PATH + wasOpenMetaverse.Constants.OAR.OBJECT_FILE_PREFIX +
                                    primitiveObject.Key + @".xml", primitiveObject.Value);

                            // Archive the asset data.
                            foreach (var asset in assetsData)
                                tarArchiveWriter.WriteFile(ArchiveConstants.ASSETS_PATH + asset.Key, asset.Value);
                            tarArchiveWriter.Close();
                        }

                        // Base64-encode the zip stream and send it.
                        tarMemoryStream.Position = 0;

                        // If no path was specificed, then send the data.
                        var path =
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PATH)),
                                corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(path))
                        {
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                Convert.ToBase64String(tarMemoryStream.ToArray()));
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
                                tarMemoryStream.WriteTo(streamWriter.BaseStream);
                                tarMemoryStream.Flush();
                            }
                        }
                    }
                };
        }
    }
}