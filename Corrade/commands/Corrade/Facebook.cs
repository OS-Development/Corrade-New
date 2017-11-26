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
using System.Threading;
using Corrade.Constants;
using CorradeConfigurationSharp;
using Facebook;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Imaging;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> facebook =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Talk))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                    /*
                    * Create a new application (canvas is the way to go) and then generate
                    * an user access token: https://developers.facebook.com/tools/explorer
                    * using the Graph API explorer whilst granting appropriate permissions.
                    */
                    var accessToken = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TOKEN)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(accessToken))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ACCESS_TOKEN_PROVIDED);

                    var client = new FacebookClient(accessToken);

                    switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                    ))
                    {
                        case Enumerations.Action.POST:
                            var facebookPostObject = new Dictionary<string, object>();
                            var message = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MESSAGE)),
                                    corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(message))
                                facebookPostObject.Add("message", message);
                            var name = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                                    corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(name))
                                facebookPostObject.Add("name", name);
                            var link = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.URL)),
                                    corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(link))
                                facebookPostObject.Add("link", link);
                            var description = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                    corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(description))
                                facebookPostObject.Add("description", description);
                            var item = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                                    corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(item))
                            {
                                var facebookMediaObject = new FacebookMediaObject();
                                UUID itemUUID;
                                // If the asset is of an asset type that can only be retrieved locally or the item is a string
                                // then attempt to resolve the item to an inventory item or else the item cannot be found.
                                switch (!UUID.TryParse(item, out itemUUID))
                                {
                                    case true:
                                        var inventoryItem =
                                            Inventory.FindInventory<InventoryItem>(Client, item,
                                                CORRADE_CONSTANTS.PATH_SEPARATOR,
                                                CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                                corradeConfiguration.ServicesTimeout);
                                        if (inventoryItem == null)
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                                        itemUUID = inventoryItem.AssetUUID;
                                        facebookMediaObject.FileName = inventoryItem.Name;
                                        break;

                                    default:
                                        facebookMediaObject.FileName = itemUUID.ToString();
                                        break;
                                }
                                Locks.ClientInstanceAssetsLock.EnterReadLock();
                                var cacheHasAsset = Client.Assets.Cache.HasAsset(itemUUID);
                                Locks.ClientInstanceAssetsLock.ExitReadLock();
                                byte[] assetData = null;
                                switch (!cacheHasAsset)
                                {
                                    case true:
                                        var RequestAssetEvent = new ManualResetEventSlim(false);
                                        Locks.ClientInstanceAssetsLock.EnterReadLock();
                                        Client.Assets.RequestImage(itemUUID, ImageType.Normal,
                                            delegate(TextureRequestState state, AssetTexture asset)
                                            {
                                                if (!asset.AssetID.Equals(itemUUID)) return;
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
                                        break;

                                    default:
                                        Locks.ClientInstanceAssetsLock.EnterReadLock();
                                        assetData = Client.Assets.Cache.GetCachedAssetBytes(itemUUID);
                                        Locks.ClientInstanceAssetsLock.ExitReadLock();
                                        break;
                                }
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
                                            var encoderParameters = new EncoderParameters(1);
                                            encoderParameters.Param[0] =
                                                new EncoderParameter(Encoder.Quality, 100L);
                                            bitmapImage.Save(imageStream,
                                                ImageCodecInfo.GetImageDecoders()
                                                    .AsParallel()
                                                    .FirstOrDefault(o => o.FormatID.Equals(ImageFormat.Jpeg.Guid)),
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
                                facebookMediaObject.SetValue(assetData);
                                facebookMediaObject.ContentType = "image/jpg";
                                facebookPostObject.Add("image", facebookMediaObject);
                            }
                            if (!facebookPostObject.Any())
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_DATA_PROVIDED);
                            var id = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ID)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(id))
                                id = "/me";
                            switch (!string.IsNullOrEmpty(item))
                            {
                                case true:
                                    client.Post(id + "/photos", facebookPostObject);
                                    break;

                                default:
                                    client.Post(id + "/feed", facebookPostObject);
                                    break;
                            }
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}