///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Corrade.Events;
using CorradeConfiguration;
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
            public static Action<Command.CorradeCommandParameters, Dictionary<string, string>> attach =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var attachments =
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ATTACHMENTS)),
                                corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(attachments))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.EMPTY_ATTACHMENTS);
                    }
                    bool replace;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REPLACE)),
                                    corradeCommandParameters.Message)),
                            out replace))
                    {
                        replace = true;
                    }
                    var items = CSV.ToKeyValue(attachments)
                        .ToDictionary(o => o.Key, o => o.Value);
                    // if this is SecondLife, check that the additional attachments would not exceed the maximum attachment limit
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client))
                    {
                        switch (replace)
                        {
                            case true:
                                if (
                                    Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                                        .Count() + items.Count -
                                    typeof (AttachmentPoint).GetFields(
                                        BindingFlags.Public | BindingFlags.Static)
                                        .AsParallel().Count(p => !items.ContainsKey(p.Name)) >
                                    wasOpenMetaverse.Constants.AVATARS.MAXIMUM_NUMBER_OF_ATTACHMENTS)
                                {
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.ATTACHMENTS_WOULD_EXCEED_MAXIMUM_ATTACHMENT_LIMIT);
                                }
                                break;
                            default:
                                if (items.Count +
                                    Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                                        .Count() >
                                    wasOpenMetaverse.Constants.AVATARS.MAXIMUM_NUMBER_OF_ATTACHMENTS)
                                {
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.ATTACHMENTS_WOULD_EXCEED_MAXIMUM_ATTACHMENT_LIMIT);
                                }
                                break;
                        }
                    }

                    // stop non default animations if requested
                    bool deanimate;
                    switch (bool.TryParse(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DEANIMATE)),
                            corradeCommandParameters.Message)), out deanimate) && deanimate)
                    {
                        case true:
                            // stop all non-built-in animations
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.SignaledAnimations.Copy()
                                    .Keys.AsParallel()
                                    .Where(o => !wasOpenMetaverse.Helpers.LindenAnimations.Contains(o))
                                    .ForAll(o => { Client.Self.AnimationStop(o, true); });
                            }
                            break;
                    }

                    items.AsParallel().ForAll(o =>
                        typeof (AttachmentPoint).GetFields(BindingFlags.Public | BindingFlags.Static)
                            .AsParallel().Where(
                                p =>
                                    Strings.Equals(o.Key, p.Name, StringComparison.Ordinal)).ForAll(
                                        q =>
                                        {
                                            InventoryItem inventoryItem;
                                            UUID itemUUID;
                                            switch (UUID.TryParse(o.Value, out itemUUID))
                                            {
                                                case true:
                                                    inventoryItem = Inventory.FindInventory<InventoryBase>(Client,
                                                        Client.Inventory.Store.RootNode, itemUUID,
                                                        corradeConfiguration.ServicesTimeout
                                                        ).FirstOrDefault() as InventoryItem;
                                                    break;
                                                default:
                                                    inventoryItem =
                                                        Inventory.FindInventory<InventoryBase>(Client,
                                                            Client.Inventory.Store.RootNode, o.Value,
                                                            corradeConfiguration.ServicesTimeout)
                                                            .FirstOrDefault() as InventoryItem;
                                                    break;
                                            }
                                            if (inventoryItem == null)
                                                return;

                                            if (inventoryItem is InventoryObject || inventoryItem is InventoryAttachment)
                                            {
                                                Inventory.Attach(Client, CurrentOutfitFolder,
                                                    inventoryItem,
                                                    (AttachmentPoint) q.GetValue(null),
                                                    replace, corradeConfiguration.ServicesTimeout);
                                                var slot = Inventory.GetAttachments(
                                                    Client,
                                                    corradeConfiguration.DataTimeout)
                                                    .ToArray()
                                                    .AsParallel()
                                                    .Where(
                                                        p =>
                                                            p.Key.Properties.ItemID.Equals(
                                                                inventoryItem.UUID))
                                                    .Select(p => p.Value.ToString())
                                                    .FirstOrDefault() ?? AttachmentPoint.Default.ToString();
                                                CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                                    () => SendNotification(
                                                        Configuration.Notifications.OutfitChanged,
                                                        new OutfitEventArgs
                                                        {
                                                            Action = Enumerations.Action.ATTACH,
                                                            Name = inventoryItem.Name,
                                                            Description = inventoryItem.Description,
                                                            Item = inventoryItem.UUID,
                                                            Asset = inventoryItem.AssetUUID,
                                                            Entity = inventoryItem.AssetType,
                                                            Creator = inventoryItem.CreatorID,
                                                            Permissions =
                                                                Inventory.wasPermissionsToString(
                                                                    inventoryItem.Permissions),
                                                            Inventory = inventoryItem.InventoryType,
                                                            Replace = replace,
                                                            Slot = slot
                                                        }),
                                                    corradeConfiguration.MaximumNotificationThreads);
                                            }
                                        }));
                    RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                };
        }
    }
}