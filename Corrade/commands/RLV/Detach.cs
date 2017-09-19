///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using Corrade.Events;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using Inventory = wasOpenMetaverse.Inventory;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static readonly Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> detach =
                (message, rule, senderUUID) =>
                {
                    if (!rule.Param.Equals(wasOpenMetaverse.RLV.RLV_CONSTANTS.FORCE))
                    {
                        return;
                    }
                    var RLVFolder = Inventory.FindInventory<InventoryFolder>(Client,
                        wasOpenMetaverse.RLV.RLV_CONSTANTS.SHARED_FOLDER_PATH,
                        wasOpenMetaverse.RLV.RLV_CONSTANTS.PATH_SEPARATOR, null, corradeConfiguration.ServicesTimeout,
                        Client.Inventory.Store.RootFolder);
                    if (RLVFolder == null)
                    {
                        return;
                    }

                    lock (RLVInventoryLock)
                    {
                        // get current attachments.
                        var currentAttachments = Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                            .ToDictionary(o => o.Key.Properties.ItemID, o => o.Value.ToString());
                        switch (!string.IsNullOrEmpty(rule.Option))
                        {
                            case true:
                                var RLVattachment = wasOpenMetaverse.RLV.RLVAttachments.AsParallel().FirstOrDefault(
                                    o =>
                                        string.Equals(rule.Option, o.Name,
                                            StringComparison.InvariantCultureIgnoreCase));
                                switch (!RLVattachment.Equals(default(wasOpenMetaverse.RLV.RLVAttachment)))
                                {
                                    case true: // detach by attachment point
                                        Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                                            .AsParallel()
                                            .Where(o => o.Value.Equals(RLVattachment.AttachmentPoint))
                                            .Select(
                                                o =>
                                                    new
                                                    {
                                                        Item = Inventory.GetAttachedInventoryItem(Client, o.Key),
                                                        Slot = o.Value.ToString(),
                                                        Attachment = o.Key.ID
                                                    })
                                            .Where(
                                                o =>
                                                    o.Item != null && 
                                                    (o.Item is InventoryAttachment || o.Item is InventoryObject) &&
                                                    // block detach for RLVa "nostrip"
                                                    (!o.Item.Name.Contains(wasOpenMetaverse.RLV.RLV_CONSTANTS.NOSTRIP)))
                                            .ForAll(o =>
                                            {
                                                // block detach if RLV detach rule forbids it
                                                var slot = string.Empty;
                                                if (!currentAttachments.TryGetValue(o.Item.UUID, out slot))
                                                    return;

                                                if (RLVRules.AsParallel().Any(p => p.Behaviour.Equals(
                                                         Reflection.GetNameFromEnumValue(RLV.RLVBehaviour.DETACH)) &&
                                                         (string.Equals(slot, p.Param, StringComparison.OrdinalIgnoreCase) || 
                                                         p.ObjectUUID.Equals(o.Attachment))))
                                                    return;

                                                // block detach for RLVa parent folder "nostrip"
                                                Locks.ClientInstanceInventoryLock.EnterReadLock();
                                                if (Client.Inventory.Store.Contains(o.Item.ParentUUID) &&
                                                    Client.Inventory.Store[o.Item.ParentUUID].Name.Contains(wasOpenMetaverse.RLV.RLV_CONSTANTS.NOSTRIP))
                                                {
                                                    Locks.ClientInstanceInventoryLock.ExitReadLock();
                                                    return;
                                                }
                                                Locks.ClientInstanceInventoryLock.ExitReadLock();

                                                // detach the object
                                                CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                                    () => SendNotification(
                                                        Configuration.Notifications.OutfitChanged,
                                                        new OutfitEventArgs
                                                        {
                                                            Action = Enumerations.Action.DETACH,
                                                            Name = o.Item.Name,
                                                            Description = o.Item.Description,
                                                            Item = o.Item.UUID,
                                                            Asset = o.Item.AssetUUID,
                                                            Entity = o.Item.AssetType,
                                                            Creator = o.Item.CreatorID,
                                                            Permissions =
                                                                Inventory.wasPermissionsToString(
                                                                    o.Item.Permissions),
                                                            Inventory = o.Item.InventoryType,
                                                            Slot = o.Slot
                                                        }),
                                                    corradeConfiguration.MaximumNotificationThreads);
                                                Inventory.Detach(Client, CurrentOutfitFolder, o.Item,
                                                    corradeConfiguration.ServicesTimeout);
                                            });
                                        break;

                                    default: // detach by folder(s) name
                                        var inventoryFolder = Inventory.FindInventory<InventoryFolder>(Client,
                                            rule.Option,
                                            wasOpenMetaverse.RLV.RLV_CONSTANTS.PATH_SEPARATOR, null,
                                            corradeConfiguration.ServicesTimeout, RLVFolder,
                                            StringComparison.OrdinalIgnoreCase);

                                        if (inventoryFolder == null)
                                            return;

                                        // block detach for RLVa parent folder "nostrip"
                                        if (inventoryFolder.Name.Contains(wasOpenMetaverse.RLV.RLV_CONSTANTS.NOSTRIP))
                                            return;

                                        Inventory.FolderContents(Client, inventoryFolder.UUID,
                                            Client.Self.AgentID,
                                            false,
                                            true,
                                            InventorySortOrder.ByDate, (int)corradeConfiguration.ServicesTimeout)
                                            .AsParallel()
                                            .Where(o => 
                                                Inventory.CanBeWorn(o) &&
                                                // block detach for RLVa "nostrip"
                                                !o.Name.Contains(wasOpenMetaverse.RLV.RLV_CONSTANTS.NOSTRIP)
                                            ).ForAll(
                                                o =>
                                                {
                                                    var inventoryItem = o as InventoryItem;
                                                    if (inventoryItem is InventoryWearable)
                                                    {
                                                        CorradeThreadPool[
                                                            Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                                                () => SendNotification(
                                                                    Configuration.Notifications.OutfitChanged,
                                                                    new OutfitEventArgs
                                                                    {
                                                                        Action = Enumerations.Action.DETACH,
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
                                                                        Slot =
                                                                            (inventoryItem as InventoryWearable)
                                                                                .WearableType.ToString()
                                                                    }),
                                                                corradeConfiguration.MaximumNotificationThreads);
                                                        Inventory.UnWear(Client, CurrentOutfitFolder,
                                                            inventoryItem, corradeConfiguration.ServicesTimeout);
                                                        return;
                                                    }
                                                    if (inventoryItem is InventoryAttachment ||
                                                        inventoryItem is InventoryObject)
                                                    {
                                                        var slot = string.Empty;
                                                        if (!currentAttachments.TryGetValue(inventoryItem.UUID, out slot))
                                                        {
                                                            return;
                                                        }
                                                        CorradeThreadPool[
                                                            Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                                                () => SendNotification(
                                                                    Configuration.Notifications.OutfitChanged,
                                                                    new OutfitEventArgs
                                                                    {
                                                                        Action = Enumerations.Action.DETACH,
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
                                                                        Slot = slot
                                                                    }),
                                                                corradeConfiguration.MaximumNotificationThreads);
                                                        Inventory.Detach(Client, CurrentOutfitFolder, inventoryItem,
                                                            corradeConfiguration.ServicesTimeout);
                                                    }
                                                });
                                        break;
                                }
                                break;

                            default:
                                Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                                    .AsParallel()
                                    .Where(
                                        o =>
                                            wasOpenMetaverse.RLV.RLVAttachments.Any(
                                                p => p.AttachmentPoint.Equals(o.Value)))
                                    .Select(o => Inventory.GetAttachedInventoryItem(Client, o.Key))
                                    .Where(o => o != null &&
                                        (o is InventoryAttachment || o is InventoryObject) &&
                                        // block detach for RLVa "nostrip"
                                        (!o.Name.Contains(wasOpenMetaverse.RLV.RLV_CONSTANTS.NOSTRIP)))
                                    .ForAll(
                                        o =>
                                        {
                                            var slot = string.Empty;
                                            if (!currentAttachments.TryGetValue(o.UUID, out slot))
                                                return;

                                            // block detach for RLVa parent folder "nostrip"
                                            Locks.ClientInstanceInventoryLock.EnterReadLock();
                                            if (Client.Inventory.Store.Contains(o.ParentUUID) &&
                                                Client.Inventory.Store[o.ParentUUID].Name.Contains(wasOpenMetaverse.RLV.RLV_CONSTANTS.NOSTRIP))
                                            {
                                                Locks.ClientInstanceInventoryLock.ExitReadLock();
                                                return;
                                            }
                                            Locks.ClientInstanceInventoryLock.ExitReadLock();

                                            CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                                () => SendNotification(
                                                    Configuration.Notifications.OutfitChanged,
                                                    new OutfitEventArgs
                                                    {
                                                        Action = Enumerations.Action.DETACH,
                                                        Name = o.Name,
                                                        Description = o.Description,
                                                        Item = o.UUID,
                                                        Asset = o.AssetUUID,
                                                        Entity = o.AssetType,
                                                        Creator = o.CreatorID,
                                                        Permissions =
                                                            Inventory.wasPermissionsToString(
                                                                o.Permissions),
                                                        Inventory = o.InventoryType,
                                                        Slot = slot
                                                    }),
                                                corradeConfiguration.MaximumNotificationThreads);
                                            Inventory.Detach(Client, CurrentOutfitFolder, o,
                                                corradeConfiguration.ServicesTimeout);
                                        });
                                break;
                        }

                        // stop all non-built-in animations
                        Locks.ClientInstanceSelfLock.EnterWriteLock();
                        Client.Self.SignaledAnimations.Copy()
                                .Keys.AsParallel()
                                .Where(o => !wasOpenMetaverse.Helpers.LindenAnimations.Contains(o))
                                .ForAll(o => { Client.Self.AnimationStop(o, true); });
                        Locks.ClientInstanceSelfLock.ExitWriteLock();

                        RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                    }
                };
        }
    }
}
