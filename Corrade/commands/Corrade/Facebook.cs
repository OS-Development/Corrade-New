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
using System.Threading;
using CorradeConfiguration;
using Facebook;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Imaging;
using wasOpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> facebook =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Talk))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }

                    /*
                    * Create a new application (canvas is the way to go) and then generate
                    * an user access token: https://developers.facebook.com/tools/explorer
                    * using the Graph API explorer whilst granting appropriate permissions.
                    */
                    string accessToken = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TOKEN)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(accessToken))
                    {
                        throw new ScriptException(ScriptError.NO_ACCESS_TOKEN_PROVIDED);
                    }

                    FacebookClient client = new FacebookClient(accessToken);

                    switch (Reflection.GetEnumValueFromName<Action>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Action.POST:
                            Dictionary<string, object> facebookPostObject = new Dictionary<string, object>();
                            string message = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE)),
                                    corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(message))
                            {
                                facebookPostObject.Add("message", message);
                            }
                            string name = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.NAME)),
                                    corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(name))
                            {
                                facebookPostObject.Add("name", name);
                            }
                            string link = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.URL)),
                                    corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(link))
                            {
                                facebookPostObject.Add("link", link);
                            }
                            string description = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DESCRIPTION)),
                                    corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(description))
                            {
                                facebookPostObject.Add("description", description);
                            }
                            string item = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
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
                                        InventoryItem inventoryItem = Inventory.FindInventory<InventoryBase>(Client,
                                            Client.Inventory.Store.RootNode, item)
                                            .FirstOrDefault() as InventoryItem;
                                        if (inventoryItem == null)
                                        {
                                            throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                                        }
                                        itemUUID = inventoryItem.AssetUUID;
                                        facebookMediaObject.FileName = inventoryItem.Name;
                                        break;
                                    default:
                                        facebookMediaObject.FileName = itemUUID.ToString();
                                        break;
                                }
                                bool cacheHasAsset;
                                lock (Locks.ClientInstanceAssetsLock)
                                {
                                    cacheHasAsset = Client.Assets.Cache.HasAsset(itemUUID);
                                }
                                byte[] assetData = null;
                                switch (!cacheHasAsset)
                                {
                                    case true:
                                        ManualResetEvent RequestAssetEvent = new ManualResetEvent(false);
                                        lock (Locks.ClientInstanceAssetsLock)
                                        {
                                            Client.Assets.RequestImage(itemUUID, ImageType.Normal,
                                                delegate(TextureRequestState state, AssetTexture asset)
                                                {
                                                    if (!asset.AssetID.Equals(itemUUID)) return;
                                                    if (!state.Equals(TextureRequestState.Finished)) return;
                                                    assetData = asset.AssetData;
                                                    RequestAssetEvent.Set();
                                                });
                                            if (
                                                !RequestAssetEvent.WaitOne((int) corradeConfiguration.ServicesTimeout,
                                                    false))
                                            {
                                                throw new ScriptException(ScriptError.TIMEOUT_TRANSFERRING_ASSET);
                                            }
                                        }
                                        break;
                                    default:
                                        lock (Locks.ClientInstanceAssetsLock)
                                        {
                                            assetData = Client.Assets.Cache.GetCachedAssetBytes(itemUUID);
                                        }
                                        break;
                                }
                                ManagedImage managedImage;
                                if (!OpenJPEG.DecodeToImage(assetData, out managedImage))
                                {
                                    throw new ScriptException(ScriptError.UNABLE_TO_DECODE_ASSET_DATA);
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
                                                    .FirstOrDefault(o => o.FormatID.Equals(ImageFormat.Jpeg.Guid)),
                                                encoderParameters);
                                        }
                                    }
                                    catch (Exception)
                                    {
                                        throw new ScriptException(ScriptError.UNABLE_TO_CONVERT_TO_REQUESTED_FORMAT);
                                    }
                                    assetData = imageStream.ToArray();
                                }
                                facebookMediaObject.SetValue(assetData);
                                facebookMediaObject.ContentType = "image/jpg";
                                facebookPostObject.Add("image", facebookMediaObject);
                            }
                            if (!facebookPostObject.Any())
                            {
                                throw new ScriptException(ScriptError.NO_DATA_PROVIDED);
                            }
                            string id = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ID)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(id))
                            {
                                id = "/me";
                            }
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
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}