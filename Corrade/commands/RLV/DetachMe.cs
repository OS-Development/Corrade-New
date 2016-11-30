///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Events;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static readonly Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> detachme =
                (message, rule, senderUUID) =>
                {
                    if (!rule.Param.Equals(wasOpenMetaverse.RLV.RLV_CONSTANTS.FORCE))
                    {
                        return;
                    }
                    var attachment =
                        Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                            .ToArray()
                            .AsParallel().FirstOrDefault(o => o.Key.ID.Equals(senderUUID));
                    switch (!attachment.Equals(default(KeyValuePair<Primitive, AttachmentPoint>)))
                    {
                        case true:
                            InventoryBase inventoryBase = null;
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                if (Client.Inventory.Store.Contains(attachment.Key.Properties.ItemID))
                                {
                                    inventoryBase = Client.Inventory.Store[attachment.Key.Properties.ItemID];
                                }
                            }
                            if (inventoryBase is InventoryAttachment || inventoryBase is InventoryObject)
                            {
                                var inventoryItem = inventoryBase as InventoryItem;
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
                            RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                            break;
                        default:
                            return;
                    }
                };
        }
    }
}