using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> addclassified =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Grooming) ||
                        !HasCorradePermission(commandGroup.Name, (int) Permissions.Economy))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    object item =
                        StringOrUUID(
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)),
                                message)));
                    UUID textureUUID = UUID.Zero;
                    if (item != null)
                    {
                        InventoryBase inventoryBaseItem =
                            FindInventory<InventoryBase>(Client.Inventory.Store.RootNode, item
                                ).FirstOrDefault();
                        if (inventoryBaseItem == null)
                        {
                            throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                        }
                        textureUUID = inventoryBaseItem.UUID;
                    }
                    string name =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.NAME)),
                            message));
                    if (string.IsNullOrEmpty(name))
                    {
                        throw new ScriptException(ScriptError.EMPTY_CLASSIFIED_NAME);
                    }
                    string classifiedDescription =
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DESCRIPTION)),
                                message));
                    ManualResetEvent AvatarClassifiedReplyEvent = new ManualResetEvent(false);
                    UUID classifiedUUID = UUID.Zero;
                    int classifiedCount = 0;
                    EventHandler<AvatarClassifiedReplyEventArgs> AvatarClassifiedEventHandler = (sender, args) =>
                    {
                        classifiedCount = args.Classifieds.Count;
                        KeyValuePair<UUID, string> classified = args.Classifieds.AsParallel().FirstOrDefault(
                            o =>
                                o.Value.Equals(name, StringComparison.Ordinal));
                        if (!classified.Equals(default(KeyValuePair<UUID, string>)))
                            classifiedUUID = classified.Key;
                        AvatarClassifiedReplyEvent.Set();
                    };
                    lock (ClientInstanceAvatarsLock)
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
                    if (IsSecondLife() &&
                        classifiedUUID.Equals(UUID.Zero) &&
                        classifiedCount >= LINDEN_CONSTANTS.AVATARS.CLASSIFIEDS.MAXIMUM_CLASSIFIEDS)
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
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.PRICE)), message)),
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
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.RENEW)), message)),
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
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)),
                                        message)),
                                StringComparison.Ordinal));
                    Client.Self.UpdateClassifiedInfo(classifiedUUID, classifiedCategoriesField != null
                        ? (DirectoryManager.ClassifiedCategories)
                            classifiedCategoriesField.GetValue(null)
                        : DirectoryManager.ClassifiedCategories.Any, textureUUID, price,
                        name, classifiedDescription, renew);
                };
        }
    }
}