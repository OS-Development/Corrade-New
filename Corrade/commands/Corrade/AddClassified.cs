///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using Corrade.Constants;
using CorradeConfigurationSharp;
using OpenMetaverse;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> addclassified =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming) ||
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Economy))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    Vector3d position;
                    if (
                        !Vector3d.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                corradeCommandParameters.Message)),
                            out position))
                        position = Client.Self.GlobalPosition;
                    var item =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                                corradeCommandParameters.Message));
                    var textureUUID = UUID.Zero;
                    if (!string.IsNullOrEmpty(item))
                        if (!UUID.TryParse(item, out textureUUID))
                        {
                            var inventoryBaseItem = Inventory.FindInventory<InventoryBase>(Client, item,
                                CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                corradeConfiguration.ServicesTimeout);
                            if (!(inventoryBaseItem is InventoryTexture))
                                throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                            textureUUID = (inventoryBaseItem as InventoryTexture).AssetUUID;
                        }
                    var name =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                        throw new Command.ScriptException(Enumerations.ScriptError.EMPTY_CLASSIFIED_NAME);
                    var classifiedDescription =
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                corradeCommandParameters.Message));
                    var AvatarClassifiedReplyEvent = new ManualResetEventSlim(false);
                    var classifiedUUID = UUID.Zero;
                    var classifiedCount = 0;
                    EventHandler<AvatarClassifiedReplyEventArgs> AvatarClassifiedEventHandler = (sender, args) =>
                    {
                        if (!args.AvatarID.Equals(Client.Self.AgentID))
                            return;

                        classifiedCount = args.Classifieds.Count;
                        var classified = args.Classifieds.AsParallel().FirstOrDefault(
                            o =>
                                string.Equals(name, o.Value, StringComparison.Ordinal));
                        if (!classified.Equals(default(KeyValuePair<UUID, string>)))
                            classifiedUUID = classified.Key;
                        AvatarClassifiedReplyEvent.Set();
                    };
                    Locks.ClientInstanceAvatarsLock.EnterReadLock();
                    Client.Avatars.AvatarClassifiedReply += AvatarClassifiedEventHandler;
                    Client.Avatars.RequestAvatarClassified(Client.Self.AgentID);
                    if (!AvatarClassifiedReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedEventHandler;
                        Locks.ClientInstanceAvatarsLock.ExitReadLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_CLASSIFIEDS);
                    }
                    Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedEventHandler;
                    Locks.ClientInstanceAvatarsLock.ExitReadLock();
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                        classifiedUUID.Equals(UUID.Zero) &&
                        classifiedCount >= wasOpenMetaverse.Constants.AVATARS.CLASSIFIEDS.MAXIMUM_CLASSIFIEDS)
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.MAXIMUM_AMOUNT_OF_CLASSIFIEDS_REACHED);
                    if (classifiedUUID.Equals(UUID.Zero))
                        classifiedUUID = UUID.Random();
                    uint price;
                    if (
                        !uint.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PRICE)),
                                corradeCommandParameters.Message)), NumberStyles.Currency, Utils.EnUsCulture,
                            out price))
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_PRICE);
                    bool renew;
                    if (
                        !bool.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RENEW)),
                                corradeCommandParameters.Message)),
                            out renew))
                        renew = false;
                    var classifiedCategoriesField = typeof(DirectoryManager.ClassifiedCategories).GetFields(
                            BindingFlags.Public |
                            BindingFlags.Static)
                        .AsParallel().FirstOrDefault(o =>
                            o.Name.Equals(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                        corradeCommandParameters.Message)),
                                StringComparison.Ordinal));
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.UpdateClassifiedInfo(classifiedUUID, classifiedCategoriesField != null
                            ? (DirectoryManager.ClassifiedCategories)
                            classifiedCategoriesField.GetValue(null)
                            : DirectoryManager.ClassifiedCategories.Any, textureUUID, (int) price, position,
                        name, classifiedDescription, renew);
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                };
        }
    }
}