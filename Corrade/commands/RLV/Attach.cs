///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Corrade.Events;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> attach = (message, rule, senderUUID) =>
            {
                if (!rule.Param.Equals(wasOpenMetaverse.RLV.RLV_CONSTANTS.FORCE) || string.IsNullOrEmpty(rule.Option))
                {
                    return;
                }
                var RLVFolder =
                    Inventory.FindInventory<InventoryNode>(Client, Client.Inventory.Store.RootNode,
                        wasOpenMetaverse.RLV.RLV_CONSTANTS.SHARED_FOLDER_NAME, corradeConfiguration.ServicesTimeout)
                        .ToArray()
                        .AsParallel()
                        .FirstOrDefault(o => o.Data is InventoryFolder);
                if (RLVFolder == null)
                {
                    return;
                }
                var attachmentInventoryBases =
                    new List<InventoryBase>(rule.Option.Split(wasOpenMetaverse.RLV.RLV_CONSTANTS.PATH_SEPARATOR[0])
                        .AsParallel().Select(
                            p =>
                                Inventory.FindInventory<InventoryBase>(Client, RLVFolder,
                                    new Regex(Regex.Escape(p),
                                        RegexOptions.Compiled | RegexOptions.IgnoreCase),
                                    corradeConfiguration.ServicesTimeout
                                    ).AsParallel().FirstOrDefault(o => o is InventoryFolder)).Where(o => o != null));
                var wearableAttachments = new List<InventoryBase>();
                lock (Locks.ClientInstanceInventoryLock)
                {
                    wearableAttachments.AddRange(attachmentInventoryBases
                        .SelectMany(
                            o =>
                                Client.Inventory.Store.GetContents(o as InventoryFolder)
                                    .AsParallel()
                                    .Where(Inventory.CanBeWorn)));
                }
                wearableAttachments.AsParallel().ForAll(o =>
                {
                    var inventoryItem = o as InventoryItem;
                    if (inventoryItem is InventoryWearable)
                    {
                        Inventory.Wear(Client, CurrentOutfitFolder, inventoryItem, true,
                            corradeConfiguration.ServicesTimeout);
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
                                    Slot = (inventoryItem as InventoryWearable).WearableType.ToString(),
                                    Replace = true
                                }),
                            corradeConfiguration.MaximumNotificationThreads);
                        return;
                    }
                    if (inventoryItem is InventoryObject || inventoryItem is InventoryAttachment)
                    {
                        Inventory.Attach(Client, CurrentOutfitFolder, inventoryItem,
                            AttachmentPoint.Default, true, corradeConfiguration.ServicesTimeout);
                        var slot = Inventory.GetAttachments(
                            Client,
                            corradeConfiguration.DataTimeout)
                            .ToArray()
                            .AsParallel()
                            .Where(p => p.Key.Properties.ItemID.Equals(inventoryItem.UUID))
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
                                    Slot = slot,
                                    Replace = true
                                }),
                            corradeConfiguration.MaximumNotificationThreads);
                    }
                });
                RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
            };
        }
    }
}