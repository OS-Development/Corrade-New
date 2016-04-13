///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Helpers = wasOpenMetaverse.Helpers;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> addclassified =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming) ||
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Economy))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Vector3d position;
                    if (
                        !Vector3d.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION)),
                                corradeCommandParameters.Message)),
                            out position))
                    {
                        position = Client.Self.GlobalPosition;
                    }
                    string item =
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                                    corradeCommandParameters.Message));
                    UUID textureUUID = UUID.Zero;
                    if (!string.IsNullOrEmpty(item))
                    {
                        // if the item is an UUID, trust the sender otherwise search the inventory
                        if (!UUID.TryParse(item, out textureUUID))
                        {
                            InventoryBase inventoryBaseItem =
                                    Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode, item
                                        ).FirstOrDefault();
                            if (!(inventoryBaseItem is InventoryTexture))
                            {
                                throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                            }
                            textureUUID = (inventoryBaseItem as InventoryTexture).AssetUUID;
                        }
                    }
                    string name =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                    {
                        throw new ScriptException(ScriptError.EMPTY_CLASSIFIED_NAME);
                    }
                    string classifiedDescription =
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DESCRIPTION)),
                                corradeCommandParameters.Message));
                    ManualResetEvent AvatarClassifiedReplyEvent = new ManualResetEvent(false);
                    UUID classifiedUUID = UUID.Zero;
                    int classifiedCount = 0;
                    EventHandler<AvatarClassifiedReplyEventArgs> AvatarClassifiedEventHandler = (sender, args) =>
                    {
                        classifiedCount = args.Classifieds.Count;
                        KeyValuePair<UUID, string> classified = args.Classifieds.AsParallel().FirstOrDefault(
                            o =>
                                string.Equals(name, o.Value, StringComparison.Ordinal));
                        if (!classified.Equals(default(KeyValuePair<UUID, string>)))
                            classifiedUUID = classified.Key;
                        AvatarClassifiedReplyEvent.Set();
                    };
                    lock (Locks.ClientInstanceAvatarsLock)
                    {
                        Client.Avatars.AvatarClassifiedReply += AvatarClassifiedEventHandler;
                        Client.Avatars.RequestAvatarClassified(Client.Self.AgentID);
                        if (!AvatarClassifiedReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_CLASSIFIEDS);
                        }
                        Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedEventHandler;
                    }
                    if (Helpers.IsSecondLife(Client) &&
                        classifiedUUID.Equals(UUID.Zero) &&
                        classifiedCount >= Constants.AVATARS.CLASSIFIEDS.MAXIMUM_CLASSIFIEDS)
                    {
                        throw new ScriptException(ScriptError.MAXIMUM_AMOUNT_OF_CLASSIFIEDS_REACHED);
                    }
                    if (classifiedUUID.Equals(UUID.Zero))
                    {
                        classifiedUUID = UUID.Random();
                    }
                    int price;
                    if (
                        !int.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.PRICE)),
                                corradeCommandParameters.Message)),
                            out price))
                    {
                        throw new ScriptException(ScriptError.INVALID_PRICE);
                    }
                    if (price < 0)
                    {
                        throw new ScriptException(ScriptError.INVALID_PRICE);
                    }
                    bool renew;
                    if (
                        !bool.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.RENEW)),
                                corradeCommandParameters.Message)),
                            out renew))
                    {
                        renew = false;
                    }
                    FieldInfo classifiedCategoriesField = typeof (DirectoryManager.ClassifiedCategories).GetFields(
                        BindingFlags.Public |
                        BindingFlags.Static)
                        .AsParallel().FirstOrDefault(o =>
                            o.Name.Equals(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                                        corradeCommandParameters.Message)),
                                StringComparison.Ordinal));
                    Client.Self.UpdateClassifiedInfo(classifiedUUID, classifiedCategoriesField != null
                        ? (DirectoryManager.ClassifiedCategories)
                            classifiedCategoriesField.GetValue(null)
                        : DirectoryManager.ClassifiedCategories.Any, textureUUID, price, position,
                        name, classifiedDescription, renew);
                };
        }
    }
}