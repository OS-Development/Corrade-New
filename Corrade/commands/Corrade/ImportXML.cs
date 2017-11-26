///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading;
using CorradeConfigurationSharp;
using OpenMetaverse;
using OpenMetaverse.Imaging;
using OpenMetaverse.StructuredData;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Timers;
using Inventory = wasOpenMetaverse.Inventory;
using Parallel = System.Threading.Tasks.Parallel;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> importxml =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                    // Get data.
                    var data =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(data))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_DATA_PROVIDED);

                    // Get permissions to apply if requested.
                    var itemPermissions =
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PERMISSIONS)),
                                corradeCommandParameters.Message));
                    var permissions = new Permissions((uint) PermissionMask.All, (uint) PermissionMask.All,
                        (uint) PermissionMask.All, (uint) PermissionMask.All, (uint) PermissionMask.All);
                    if (string.IsNullOrEmpty(itemPermissions))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_PERMISSIONS_PROVIDED);

                    if (!Inventory.wasStringToPermissions(itemPermissions, out permissions))
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_PERMISSIONS);

                    // Optional region.
                    var region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                corradeCommandParameters.Message));
                    Locks.ClientInstanceNetworkLock.EnterReadLock();
                    var simulator = Client.Network.Simulators.AsParallel().FirstOrDefault(
                        o =>
                            o.Name.Equals(
                                string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                StringComparison.OrdinalIgnoreCase));
                    Locks.ClientInstanceNetworkLock.ExitReadLock();
                    if (simulator == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);

                    // Get the position where to import the object.
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_POSITION);
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                        position.Z > wasOpenMetaverse.Constants.PRIMITIVES.MAXIMUM_REZ_HEIGHT)
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.POSITION_WOULD_EXCEED_MAXIMUM_REZ_ALTITUDE);

                    // Check build rights.
                    Parcel parcel = null;
                    if (
                        !Services.GetParcelAtPosition(Client, simulator, position,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            ref parcel))
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);

                    // Check if Corrade has permissions in the parcel group.
                    var initialGroup = Client.Self.ActiveGroup;
                    if (!simulator.IsEstateManager && !parcel.Flags.IsMaskFlagSet(ParcelFlags.CreateObjects) &&
                        !parcel.OwnerID.Equals(Client.Self.AgentID) &&
                        (!parcel.Flags.IsMaskFlagSet(ParcelFlags.CreateGroupObjects) ||
                         !Services.HasGroupPowers(Client, Client.Self.AgentID,
                             parcel.GroupID,
                             GroupPowers.AllowRez,
                             corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                             new DecayingAlarm(corradeConfiguration.DataDecayType))))
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);

                    var primitives = new List<Primitive>();
                    var textures = new Dictionary<UUID, UUID>();
                    switch (Reflection.GetEnumValueFromName<Enumerations.Type>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                corradeCommandParameters.Message))
                    ))
                    {
                        case Enumerations.Type.ZIP:
                            byte[] byteData;
                            try
                            {
                                byteData = Convert.FromBase64String(data);
                            }
                            catch (Exception)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                            }

                            // By default use the cache unless the user explicitly disables it.
                            bool useCache;
                            if (!bool.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.CACHE)),
                                        corradeCommandParameters.Message)),
                                out useCache))
                                useCache = true;

                            /*
                             * Open the data as a ZIP archive in memory and extract the files.
                             */
                            using (var zipMemoryStream = new MemoryStream(byteData))
                            {
                                using (
                                    var zipInputStream = new ZipArchive(zipMemoryStream, ZipArchiveMode.Read, true)
                                )
                                {
                                    var LockObject = new object();
                                    var scriptError = Enumerations.ScriptError.NONE;
                                    Parallel.ForEach(zipInputStream.Entries, (o, s) =>
                                    {
                                        var filename = o.Name.ToLower();
                                        var fileBasename = Path.GetFileNameWithoutExtension(filename);
                                        var fileExtension = Path.GetExtension(filename);

                                        byte[] fileBytes;
                                        using (var fileMemoryStream = new MemoryStream())
                                        {
                                            using (var itemStream = o.Open())
                                            {
                                                itemStream.CopyTo(fileMemoryStream);
                                            }
                                            fileBytes = fileMemoryStream.ToArray();
                                        }
                                        switch (fileExtension)
                                        {
                                            case ".xml":
                                                // get the primitives from the XML data.
                                                try
                                                {
                                                    primitives.AddRange(
                                                        OpenMetaverse.Helpers.OSDToPrimList(
                                                            OSDParser.DeserializeLLSDXml(
                                                                Encoding.UTF8.GetString(fileBytes))));
                                                }
                                                catch (Exception)
                                                {
                                                    scriptError = Enumerations.ScriptError.COULD_NOT_READ_XML_FILE;
                                                    s.Break();
                                                }
                                                break;

                                            default:
                                                UUID replaceTextureUUID;
                                                if (!UUID.TryParse(fileBasename, out replaceTextureUUID))
                                                    break; // skip any textures whose names are not named properly

                                                // If this is Second Life, skip any default textures.
                                                if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                                                    new[]
                                                    {
                                                        wasOpenMetaverse.Constants.TEXTURES.TEXTURE_BLANK,
                                                        wasOpenMetaverse.Constants.TEXTURES.TEXTURE_DEFAULT,
                                                        wasOpenMetaverse.Constants.TEXTURES.TEXTURE_MEDIA,
                                                        wasOpenMetaverse.Constants.TEXTURES.TEXTURE_PLYWOOD,
                                                        wasOpenMetaverse.Constants.TEXTURES.TEXTURE_TRANSPARENT,
                                                        wasOpenMetaverse.Constants.TEXTURES.DEFAULT_SCULPT
                                                    }.Contains(
                                                        replaceTextureUUID))
                                                    break;

                                                // If the user requred to trust the cache, then skip the upload in case the item has
                                                // been previously cached by Corrade.
                                                if (useCache)
                                                {
                                                    Locks.ClientInstanceAssetsLock.EnterReadLock();
                                                    if (Client.Assets.Cache.HasAsset(replaceTextureUUID))
                                                    {
                                                        Locks.ClientInstanceAssetsLock.ExitReadLock();
                                                        break;
                                                    }
                                                    Locks.ClientInstanceAssetsLock.ExitReadLock();
                                                }

                                                /*
                                                * Use ImageMagick on Windows and the .NET converter otherwise.
                                                */
                                                byte[] j2cBytes = null;
                                                switch (Utils.GetRunningPlatform())
                                                {
                                                    case Utils.Platform.Windows:
                                                        var imageMagickDLL = Assembly.LoadFile(
                                                            Path.Combine(Directory.GetCurrentDirectory(), @"libs",
                                                                "Magick.NET-Q16-HDRI-AnyCPU.dll"));
                                                        try
                                                        {
                                                            using (dynamic magickImage = imageMagickDLL
                                                                .CreateInstance(
                                                                    "ImageMagick.MagickImage",
                                                                    false, BindingFlags.CreateInstance, null,
                                                                    new[] {fileBytes}, null, null))
                                                            {
                                                                j2cBytes =
                                                                    OpenJPEG.EncodeFromImage(magickImage.ToBitmap(),
                                                                        true);
                                                            }
                                                        }
                                                        catch (Exception)
                                                        {
                                                            scriptError =
                                                                Enumerations.ScriptError.UNKNOWN_IMAGE_FORMAT_PROVIDED;
                                                            s.Break();
                                                        }
                                                        break;

                                                    default:
                                                        try
                                                        {
                                                            using (
                                                                var image =
                                                                    (Image) new ImageConverter().ConvertFrom(fileBytes))
                                                            {
                                                                using (var bitmap = new Bitmap(image))
                                                                {
                                                                    j2cBytes = OpenJPEG.EncodeFromImage(bitmap,
                                                                        true);
                                                                }
                                                            }
                                                        }
                                                        catch (Exception)
                                                        {
                                                            scriptError =
                                                                Enumerations.ScriptError.UNKNOWN_IMAGE_FORMAT_PROVIDED;
                                                            s.Break();
                                                        }

                                                        break;
                                                }
                                                // Check for economy Corrade permission.
                                                if (
                                                    !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                                        (int) Configuration.Permissions.Economy))
                                                {
                                                    scriptError = Enumerations.ScriptError.NO_CORRADE_PERMISSIONS;
                                                    s.Break();
                                                }
                                                if (
                                                    !Services.UpdateBalance(Client,
                                                        corradeConfiguration.ServicesTimeout))
                                                {
                                                    scriptError =
                                                        Enumerations.ScriptError.UNABLE_TO_OBTAIN_MONEY_BALANCE;
                                                    s.Break();
                                                }
                                                Locks.ClientInstanceSelfLock.EnterReadLock();
                                                if (Client.Self.Balance < Client.Settings.UPLOAD_COST)
                                                {
                                                    scriptError = Enumerations.ScriptError.INSUFFICIENT_FUNDS;
                                                    Locks.ClientInstanceSelfLock.ExitReadLock();
                                                    s.Break();
                                                }
                                                Locks.ClientInstanceSelfLock.ExitReadLock();
                                                // now create and upload the texture
                                                var CreateItemFromAssetEvent = new ManualResetEventSlim(false);
                                                var replaceByTextureUUID = UUID.Zero;
                                                var succeeded = false;
                                                Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                                Client.Inventory.RequestCreateItemFromAsset(j2cBytes, fileBasename,
                                                    string.Empty, AssetType.Texture, InventoryType.Texture,
                                                    Client.Inventory.FindFolderForType(AssetType.Texture),
                                                    delegate(bool completed, string status, UUID itemID,
                                                        UUID assetID)
                                                    {
                                                        succeeded = completed;
                                                        replaceByTextureUUID = assetID;
                                                        CreateItemFromAssetEvent.Set();
                                                    });
                                                if (
                                                    !CreateItemFromAssetEvent.Wait(
                                                        (int) corradeConfiguration.ServicesTimeout))
                                                {
                                                    scriptError = Enumerations.ScriptError.TIMEOUT_UPLOADING_ASSET;
                                                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                                    s.Break();
                                                }
                                                Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                                if (!succeeded)
                                                {
                                                    scriptError = Enumerations.ScriptError.ASSET_UPLOAD_FAILED;
                                                    s.Break();
                                                }
                                                if (corradeConfiguration.EnableHorde)
                                                    HordeDistributeCacheAsset(replaceByTextureUUID, j2cBytes,
                                                        Configuration.HordeDataSynchronizationOption.Add);
                                                // Finally, add the replacement texture to the dictionary.
                                                lock (LockObject)
                                                {
                                                    textures.Add(replaceTextureUUID, replaceByTextureUUID);
                                                }
                                                break;
                                        }
                                    });
                                    if (!scriptError.Equals(default(Enumerations.ScriptError)))
                                        throw new Command.ScriptException(scriptError);
                                }
                            }
                            break;

                        case Enumerations.Type.XML:
                            // get the primitives from the XML data.
                            try
                            {
                                primitives.AddRange(
                                    OpenMetaverse.Helpers.OSDToPrimList(
                                        OSDParser.DeserializeLLSDXml(data)));
                            }
                            catch (Exception)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_READ_XML_FILE);
                            }
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ASSET_TYPE);
                    }

                    // Build an organized structure from the imported primitives
                    var linkSets = new Dictionary<uint, Linkset>();
                    foreach (var prim in primitives)
                    {
                        if (prim.ParentID.Equals(0))
                        {
                            if (linkSets.ContainsKey(prim.LocalID))
                            {
                                linkSets[prim.LocalID].RootPrimitive = prim;
                                continue;
                            }
                            linkSets[prim.LocalID] = new Linkset(prim);
                            continue;
                        }
                        if (!linkSets.ContainsKey(prim.ParentID))
                            linkSets[prim.ParentID] = new Linkset();

                        linkSets[prim.ParentID].ChildPrimitives.Add(prim);
                    }

                    Primitive currentPrim = null;
                    var createdPrimitives = new List<Primitive>();
                    var linkQueue = new List<uint>();
                    var LinkQueueLock = new object();
                    uint rootLocalID = 0;
                    var rezState = ImporterState.Idle;
                    var primDone = new AutoResetEvent(false);

                    EventHandler<PrimEventArgs> ObjectUpdateEventHandler = (sender, args) =>
                    {
                        var primitive = args.Prim;

                        // Skip updates for objects we did not create.
                        if (!primitive.Flags.IsMaskFlagSet(PrimFlags.CreateSelected))
                            return;

                        switch (rezState)
                        {
                            case ImporterState.RezzingParent:
                                rootLocalID = primitive.LocalID;
                                goto case ImporterState.RezzingChildren;
                            case ImporterState.RezzingChildren:
                                if (createdPrimitives.Contains(primitive))
                                    break;

                                // Set all primitive properties.
                                Client.Objects.SetPosition(args.Simulator, primitive.LocalID, position);

                                if (currentPrim.Light != null && currentPrim.Light.Intensity > 0)
                                    Client.Objects.SetLight(args.Simulator, primitive.LocalID, currentPrim.Light);

                                if (currentPrim.Flexible != null)
                                    Client.Objects.SetFlexible(args.Simulator, primitive.LocalID, currentPrim.Flexible);

                                if (currentPrim.Sculpt != null && currentPrim.Sculpt.SculptTexture != UUID.Zero)
                                    Client.Objects.SetSculpt(args.Simulator, primitive.LocalID, currentPrim.Sculpt);

                                if (currentPrim.Properties != null &&
                                    !string.IsNullOrEmpty(currentPrim.Properties.Name))
                                    Client.Objects.SetName(args.Simulator, primitive.LocalID,
                                        currentPrim.Properties.Name);

                                if (currentPrim.Properties != null &&
                                    !string.IsNullOrEmpty(currentPrim.Properties.Description))
                                    Client.Objects.SetDescription(args.Simulator, primitive.LocalID,
                                        currentPrim.Properties.Description);

                                // In case there are uploaded replacement textures, replace them.
                                // Replace default texture.
                                if (currentPrim.Textures.DefaultTexture != null)
                                    switch (
                                        currentPrim.Textures.DefaultTexture.TextureID.Equals(UUID.Zero) &&
                                        wasOpenMetaverse.Helpers.IsSecondLife(Client))
                                    {
                                        case true:
                                            currentPrim.Textures.DefaultTexture.TextureID =
                                                wasOpenMetaverse.Constants.TEXTURES.TEXTURE_BLANK;
                                            break;

                                        default:
                                            UUID defaultTextureUUID;
                                            if (textures.TryGetValue(currentPrim.Textures.DefaultTexture.TextureID,
                                                out defaultTextureUUID))
                                                currentPrim.Textures.DefaultTexture.TextureID = defaultTextureUUID;
                                            break;
                                    }
                                // Set the sculpt texture.
                                if (currentPrim.Sculpt != null)
                                {
                                    UUID sculptTextureUUID;
                                    if (textures.TryGetValue(currentPrim.Sculpt.SculptTexture,
                                        out sculptTextureUUID))
                                        currentPrim.Sculpt.SculptTexture = sculptTextureUUID;
                                }
                                // Replace face textures.
                                if (currentPrim.Textures.FaceTextures != null)
                                    foreach (var faceTexture in currentPrim.Textures.FaceTextures.Where(o => o != null))
                                        // For Second Life, replace NULL UUIDs with TEXTURE_BLANK (meaning nothing).
                                        switch (
                                            faceTexture.TextureID.Equals(UUID.Zero) &&
                                            wasOpenMetaverse.Helpers.IsSecondLife(Client))
                                        {
                                            case true:
                                                faceTexture.TextureID =
                                                    wasOpenMetaverse.Constants.TEXTURES.TEXTURE_BLANK;
                                                break;

                                            default:
                                                UUID faceTextureUUID;
                                                if (textures.TryGetValue(faceTexture.TextureID, out faceTextureUUID))
                                                    faceTexture.TextureID = faceTextureUUID;
                                                break;
                                        }
                                // Finally apply the textures to the primitive.
                                Client.Objects.SetTextures(args.Simulator, primitive.LocalID, currentPrim.Textures);

                                createdPrimitives.Add(primitive);
                                primDone.Set();
                                break;

                            case ImporterState.Linking:
                                lock (LinkQueueLock)
                                {
                                    var index = linkQueue.IndexOf(primitive.LocalID);
                                    if (index != -1)
                                    {
                                        linkQueue.RemoveAt(index);
                                        if (!linkQueue.Any())
                                            primDone.Set();
                                    }
                                }
                                break;
                        }
                    };

                    // Start import.
                    Locks.ClientInstanceObjectsLock.EnterWriteLock();
                    Client.Objects.ObjectUpdate += ObjectUpdateEventHandler;
                    // Import linksets only with a valid root primitive.
                    foreach (
                        var linkSet in linkSets.Values.AsParallel().Where(o => !o.RootPrimitive.LocalID.Equals(0)))
                    {
                        rezState = ImporterState.RezzingParent;
                        currentPrim = linkSet.RootPrimitive;

                        // Rez the structure at the designated position.
                        linkSet.RootPrimitive.Position = position;

                        // Rez the root prim with no rotation
                        var rootRotation = linkSet.RootPrimitive.Rotation;
                        linkSet.RootPrimitive.Rotation = Quaternion.Identity;

                        // Activate parcel group.
                        Locks.ClientInstanceGroupsLock.EnterWriteLock();
                        Client.Groups.ActivateGroup(parcel.GroupID);

                        // Rez the primitive.
                        Locks.ClientInstanceObjectsLock.EnterWriteLock();
                        Client.Objects.AddPrim(simulator, linkSet.RootPrimitive.PrimData,
                            corradeCommandParameters.Group.UUID,
                            linkSet.RootPrimitive.Position, linkSet.RootPrimitive.Scale,
                            linkSet.RootPrimitive.Rotation);
                        Locks.ClientInstanceObjectsLock.ExitWriteLock();

                        // Activate the initial group.
                        Client.Groups.ActivateGroup(initialGroup);
                        Locks.ClientInstanceGroupsLock.ExitWriteLock();

                        if (!primDone.WaitOne((int) corradeConfiguration.ServicesTimeout))
                        {
                            Client.Objects.ObjectUpdate -= ObjectUpdateEventHandler;
                            throw new Command.ScriptException(Enumerations.ScriptError.FAILED_REZZING_ROOT_PRIMITIVE);
                        }

                        Client.Objects.SetPosition(simulator,
                            createdPrimitives[createdPrimitives.Count - 1].LocalID, linkSet.RootPrimitive.Position);

                        rezState = ImporterState.RezzingChildren;

                        // Rez the child prims
                        foreach (var primitive in linkSet.ChildPrimitives)
                        {
                            currentPrim = primitive;
                            position = primitive.Position + linkSet.RootPrimitive.Position;

                            // Activate parcel group.
                            Locks.ClientInstanceGroupsLock.EnterWriteLock();
                            Client.Groups.ActivateGroup(parcel.GroupID);

                            // Rez the primitive.
                            Locks.ClientInstanceObjectsLock.EnterWriteLock();
                            Client.Objects.AddPrim(simulator, primitive.PrimData,
                                corradeCommandParameters.Group.UUID, position,
                                primitive.Scale, primitive.Rotation);
                            Locks.ClientInstanceObjectsLock.ExitWriteLock();

                            // Activate the initial group.
                            Client.Groups.ActivateGroup(initialGroup);
                            Locks.ClientInstanceGroupsLock.ExitWriteLock();

                            if (!primDone.WaitOne((int) corradeConfiguration.ServicesTimeout, true))
                            {
                                Client.Objects.ObjectUpdate -= ObjectUpdateEventHandler;
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.FAILED_REZZING_CHILD_PRIMITIVE);
                            }
                            Client.Objects.SetPosition(simulator,
                                createdPrimitives[createdPrimitives.Count - 1].LocalID, position);
                        }

                        // Create a list of the local IDs of the newly created prims
                        // Root prim is first in list.
                        var primitiveIDs = new List<uint>(createdPrimitives.Count) {rootLocalID};

                        switch (linkSet.ChildPrimitives.Any())
                        {
                            case true:
                                // Add the rest of the prims to the list of local IDs
                                primitiveIDs.AddRange(
                                    createdPrimitives.AsParallel().Where(prim => prim.LocalID != rootLocalID)
                                        .Select(prim => prim.LocalID));

                                linkQueue = new List<uint>(primitiveIDs);

                                // Link and set the permissions + rotation
                                rezState = ImporterState.Linking;
                                Client.Objects.LinkPrims(simulator, linkQueue);

                                if (
                                    primDone.WaitOne(
                                        (int) corradeConfiguration.DataTimeout * linkSet.ChildPrimitives.Count,
                                        false))
                                    Client.Objects.SetRotation(simulator, rootLocalID, rootRotation);
                                //else
                                //    Console.WriteLine("Warning: Failed to link {0} prims", linkQueue.Count);
                                break;

                            default:
                                Client.Objects.SetRotation(simulator, rootLocalID, rootRotation);
                                break;
                        }

                        // Set permissions on newly created prims.
                        //Client.Objects.SetPermissions(simulator, primIDs, PermissionWho.Base, permissions.BaseMask, true);
                        //Client.Objects.SetPermissions(simulator, primIDs, PermissionWho.Owner, permissions.OwnerMask, true);
                        Client.Objects.SetPermissions(simulator, primitiveIDs, PermissionWho.Group,
                            permissions.GroupMask, true);
                        Client.Objects.SetPermissions(simulator, primitiveIDs,
                            PermissionWho.Everyone,
                            permissions.EveryoneMask, true);
                        Client.Objects.SetPermissions(simulator, primitiveIDs,
                            PermissionWho.NextOwner,
                            permissions.NextOwnerMask, true);

                        rezState = ImporterState.Idle;

                        // Reset everything for the next linkset
                        createdPrimitives.Clear();
                    }
                    // Import done.
                    Client.Objects.ObjectUpdate -= ObjectUpdateEventHandler;
                    Locks.ClientInstanceObjectsLock.ExitWriteLock();
                };

            private class Linkset
            {
                public readonly List<Primitive> ChildPrimitives = new List<Primitive>();
                public Primitive RootPrimitive;

                public Linkset()
                {
                    RootPrimitive = new Primitive();
                }

                public Linkset(Primitive rootPrimitive)
                {
                    RootPrimitive = rootPrimitive;
                }
            }

            private enum ImporterState
            {
                RezzingParent,
                RezzingChildren,
                Linking,
                Idle
            }
        }
    }
}