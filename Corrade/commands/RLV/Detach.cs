///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using System.Text.RegularExpressions;
using CorradeConfiguration;
using OpenMetaverse;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> detach = (message, rule, senderUUID) =>
            {
                if (!rule.Param.Equals(RLV_CONSTANTS.FORCE))
                {
                    return;
                }
                InventoryNode RLVFolder =
                    Inventory.FindInventory<InventoryNode>(Client, Client.Inventory.Store.RootNode,
                        RLV_CONSTANTS.SHARED_FOLDER_NAME)
                        .AsParallel()
                        .FirstOrDefault(o => o.Data is InventoryFolder);
                if (RLVFolder == null)
                {
                    return;
                }
                switch (!string.IsNullOrEmpty(rule.Option))
                {
                    case true:
                        RLVAttachment RLVattachment =
                            RLVAttachments.AsParallel().FirstOrDefault(
                                o => string.Equals(rule.Option, o.Name, StringComparison.InvariantCultureIgnoreCase));
                        switch (!RLVattachment.Equals(default(RLVAttachment)))
                        {
                            case true: // detach by attachment point
                                Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                                    .ToArray()
                                    .AsParallel()
                                    .Where(o => o.Value.Equals(RLVattachment.AttachmentPoint))
                                    .SelectMany(
                                        p =>
                                            Inventory.FindInventory<InventoryBase>(Client,
                                                Client.Inventory.Store.RootNode,
                                                p.Key.Properties.Name)
                                                .ToArray()
                                                .AsParallel().Where(
                                                    o =>
                                                        o is InventoryAttachment || o is InventoryObject))
                                    .Where(o => o != null)
                                    .Select(o => o as InventoryItem).ForAll(o =>
                                    {
                                        CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                            () => SendNotification(
                                                Configuration.Notifications.OutfitChanged,
                                                new OutfitEventArgs
                                                {
                                                    Action = Action.DETACH,
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
                                                    Slot = Inventory.GetAttachments(
                                                        Client,
                                                        corradeConfiguration.DataTimeout)
                                                        .AsParallel()
                                                        .Where(
                                                            p =>
                                                                p.Key.Properties.ItemID.Equals(
                                                                    o.UUID))
                                                        .Select(p => p.Value.ToString())
                                                        .FirstOrDefault()
                                                }),
                                            corradeConfiguration.MaximumNotificationThreads);
                                        Inventory.Detach(Client, CurrentOutfitFolder, o,
                                            corradeConfiguration.ServicesTimeout);
                                    });
                                break;
                            default: // detach by folder(s) name
                                if (string.IsNullOrEmpty(rule.Option)) break;
                                rule.Option.Split(RLV_CONSTANTS.PATH_SEPARATOR[0])
                                    .AsParallel().Select(
                                        p =>
                                            Inventory.FindInventory<InventoryBase>(Client, RLVFolder,
                                                new Regex(Regex.Escape(p),
                                                    RegexOptions.Compiled | RegexOptions.IgnoreCase)
                                                ).AsParallel().FirstOrDefault(o => o is InventoryFolder))
                                    .Where(o => o != null)
                                    .SelectMany(
                                        o =>
                                            Client.Inventory.Store.GetContents(o as InventoryFolder)
                                                .AsParallel().Where(Inventory.CanBeWorn)).ForAll(o =>
                                                {
                                                    InventoryItem inventoryItem = o as InventoryItem;
                                                    if (inventoryItem is InventoryWearable)
                                                    {
                                                        CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                                            () => SendNotification(
                                                                Configuration.Notifications.OutfitChanged,
                                                                new OutfitEventArgs
                                                                {
                                                                    Action = Action.DETACH,
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
                                                                    Slot = (inventoryItem as InventoryWearable).WearableType.ToString()
                                                                }),
                                                            corradeConfiguration.MaximumNotificationThreads);
                                                        Inventory.UnWear(Client, CurrentOutfitFolder,
                                                            inventoryItem, corradeConfiguration.ServicesTimeout);
                                                        return;
                                                    }
                                                    if (o is InventoryAttachment || o is InventoryObject)
                                                    {
                                                        CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                                            () => SendNotification(
                                                                Configuration.Notifications.OutfitChanged,
                                                                new OutfitEventArgs
                                                                {
                                                                    Action = Action.DETACH,
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
                                                                    Slot = Inventory.GetAttachments(
                                                                        Client,
                                                                        corradeConfiguration.DataTimeout)
                                                                        .AsParallel()
                                                                        .Where(
                                                                            p =>
                                                                                p.Key.Properties.ItemID.Equals(
                                                                                    inventoryItem.UUID))
                                                                        .Select(p => p.Value.ToString())
                                                                        .FirstOrDefault()
                                                                }),
                                                            corradeConfiguration.MaximumNotificationThreads);
                                                        Inventory.Detach(Client, CurrentOutfitFolder, inventoryItem,
                                                            corradeConfiguration.ServicesTimeout);
                                                    }
                                                });
                                RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                                break;
                        }
                        break;
                    default:
                        Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                            .ToArray()
                            .AsParallel()
                            .Where(o => RLVAttachments.Any(p => p.AttachmentPoint.Equals(o.Value)))
                            .SelectMany(
                                o =>
                                    o.Key.NameValues.AsParallel()
                                        .Where(p => string.Equals(@"AttachItemID", p.Name, StringComparison.Ordinal)))
                            .ForAll(
                                o =>
                                {
                                    UUID itemUUID;
                                    if (UUID.TryParse(o.Value.ToString(), out itemUUID))
                                    {
                                        InventoryItem inventoryItem =
                                            Client.Inventory.Store.Items[itemUUID].Data as InventoryItem;
                                        CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                            () => SendNotification(
                                                Configuration.Notifications.OutfitChanged,
                                                new OutfitEventArgs
                                                {
                                                    Action = Action.DETACH,
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
                                                    Slot = Inventory.GetAttachments(
                                                        Client,
                                                        corradeConfiguration.DataTimeout)
                                                        .AsParallel()
                                                        .Where(
                                                            p =>
                                                                p.Key.Properties.ItemID.Equals(
                                                                    inventoryItem.UUID))
                                                        .Select(p => p.Value.ToString())
                                                        .FirstOrDefault()
                                                }),
                                            corradeConfiguration.MaximumNotificationThreads);
                                        Inventory.Detach(Client, CurrentOutfitFolder, inventoryItem,
                                            corradeConfiguration.ServicesTimeout);
                                    }
                                });
                        break;
                }
            };
        }
    }
}