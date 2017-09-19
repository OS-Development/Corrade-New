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

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static readonly Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> detachall =
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
                        var inventoryFolder = Inventory.FindInventory<InventoryFolder>(Client,
                            rule.Option,
                            wasOpenMetaverse.RLV.RLV_CONSTANTS.PATH_SEPARATOR, null,
                            corradeConfiguration.ServicesTimeout, RLVFolder,
                            StringComparison.OrdinalIgnoreCase);

                        if (inventoryFolder == null)
                            return;

                        inventoryFolder.GetInventoryRecursive(Client, corradeConfiguration.ServicesTimeout)
                            .AsParallel()
                            .Where(Inventory.CanBeWorn).ForAll(
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
                                            return;

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
