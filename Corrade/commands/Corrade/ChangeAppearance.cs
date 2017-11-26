///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Constants;
using Corrade.Events;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> changeappearance
                =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var folder = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FOLDER)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(folder))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_FOLDER_SPECIFIED);
                    InventoryFolder inventoryFolder = null;
                    UUID folderUUID;
                    switch (UUID.TryParse(folder, out folderUUID))
                    {
                        case true:
                            Locks.ClientInstanceInventoryLock.EnterReadLock();
                            if (Client.Inventory.Store.Contains(folderUUID))
                                inventoryFolder = Client.Inventory.Store[folderUUID] as InventoryFolder;
                            Locks.ClientInstanceInventoryLock.ExitReadLock();
                            break;

                        default:
                            inventoryFolder =
                                Inventory.FindInventory<InventoryFolder>(Client, folder,
                                    CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                    corradeConfiguration.ServicesTimeout);
                            break;
                    }
                    if (inventoryFolder == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.FOLDER_NOT_FOUND);

                    // Get folder contents.
                    var contents = new List<InventoryBase>();
                    Locks.ClientInstanceInventoryLock.EnterReadLock();
                    contents.AddRange(Client.Inventory.Store.GetContents(inventoryFolder));
                    Locks.ClientInstanceInventoryLock.ExitReadLock();
                    var equipItems = new List<InventoryItem>(contents
                        .AsParallel()
                        .Where(o => o is InventoryItem)
                        .Select(o => Inventory.ResolveItemLink(Client, o as InventoryItem))
                        .Where(Inventory.CanBeWorn));

                    // Check if any items are left over.
                    if (!equipItems.Any())
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_EQUIPABLE_ITEMS);

                    // stop non default animations if requested
                    bool deanimate;
                    switch (bool.TryParse(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DEANIMATE)),
                                    corradeCommandParameters.Message)), out deanimate) && deanimate)
                    {
                        case true:
                            // stop all non-built-in animations
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.SignaledAnimations.Copy()
                                .Keys.AsParallel()
                                .Where(o => !wasOpenMetaverse.Helpers.LindenAnimations.Contains(o))
                                .ForAll(o => { Client.Self.AnimationStop(o, true); });
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;
                    }

                    // Create a list of links that should be removed.
                    var removeItems = new List<UUID>();
                    var LockObject = new object();
                    var attachments = Inventory.GetAttachments(Client,
                            corradeConfiguration.DataTimeout)
                        .GroupBy(o => o.Key)
                        .ToDictionary(o => o.Key, o => o.FirstOrDefault().Value);
                    Inventory.GetCurrentOutfitFolderLinks(Client, CurrentOutfitFolder,
                        corradeConfiguration.ServicesTimeout).AsParallel().ForAll(
                        o =>
                        {
                            var inventoryItem = Inventory.ResolveItemLink(Client, o);
                            switch (Inventory.IsBodyPart(Client, o))
                            {
                                case true:
                                    if (
                                        equipItems.AsParallel()
                                            .Where(t => Inventory.IsBodyPart(Client, t))
                                            .Any(
                                                p =>
                                                    ((InventoryWearable) p).WearableType.Equals(
                                                        ((InventoryWearable) inventoryItem)
                                                        .WearableType)))
                                        goto default;
                                    break;

                                default:
                                    lock (LockObject)
                                    {
                                        removeItems.Add(o.UUID);
                                        var slot = string.Empty;
                                        if (inventoryItem is InventoryWearable)
                                        {
                                            slot = (inventoryItem as InventoryWearable).WearableType.ToString();
                                        }
                                        else if (inventoryItem is InventoryObject ||
                                                 inventoryItem is InventoryAttachment)
                                        {
                                            var a =
                                                attachments.AsParallel()
                                                    .FirstOrDefault(
                                                        p => p.Key.Properties.ItemID.Equals(inventoryItem.UUID));
                                            // Item not attached.
                                            if (a.Equals(default(KeyValuePair<Primitive, AttachmentPoint>)))
                                                break;
                                            slot = a.Value.ToString();
                                        }
                                        CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                            () => SendNotification(
                                                Configuration.Notifications.OutfitChanged,
                                                new OutfitEventArgs
                                                {
                                                    Action =
                                                        inventoryItem is InventoryObject ||
                                                        inventoryItem is InventoryAttachment
                                                            ? Enumerations.Action.DETACH
                                                            : Enumerations.Action.UNWEAR,
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
                                                    Replace = true,
                                                    Slot = slot
                                                }),
                                            corradeConfiguration.MaximumNotificationThreads);
                                    }
                                    break;
                            }
                        });

                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                    // Now remove the links.
                    Client.Inventory.Remove(removeItems, null);
                    Locks.ClientInstanceInventoryLock.ExitWriteLock();

                    // Add links to new items.
                    foreach (var inventoryItem in equipItems)
                        Inventory.AddLink(Client, inventoryItem, CurrentOutfitFolder,
                            corradeConfiguration.ServicesTimeout);

                    // And replace the outfit wit hthe new items.
                    Locks.ClientInstanceAppearanceLock.EnterWriteLock();
                    Client.Appearance.ReplaceOutfit(equipItems, false);
                    Locks.ClientInstanceAppearanceLock.ExitWriteLock();

                    // Update inventory.
                    Locks.ClientInstanceInventoryLock.EnterReadLock();
                    Client.Inventory.Store.GetNodeFor(CurrentOutfitFolder.UUID).NeedsUpdate = true;
                    Locks.ClientInstanceInventoryLock.ExitReadLock();

                    attachments = Inventory.GetAttachments(Client,
                            corradeConfiguration.DataTimeout)
                        .GroupBy(o => o.Key)
                        .ToDictionary(o => o.Key, o => o.FirstOrDefault().Value);
                    equipItems.AsParallel().Select(o => o).ForAll(o =>
                    {
                        var slot = string.Empty;
                        if (o is InventoryWearable)
                        {
                            slot = (o as InventoryWearable).WearableType.ToString();
                        }
                        else if (o is InventoryObject || o is InventoryAttachment)
                        {
                            var a =
                                attachments.AsParallel()
                                    .FirstOrDefault(
                                        p => p.Key.Properties.ItemID.Equals(o.UUID));
                            if (a.Equals(default(KeyValuePair<Primitive, AttachmentPoint>)))
                                return;

                            slot = a.Value.ToString();
                        }
                        CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                            () => SendNotification(
                                Configuration.Notifications.OutfitChanged,
                                new OutfitEventArgs
                                {
                                    Action =
                                        o is InventoryObject || o is InventoryAttachment
                                            ? Enumerations.Action.ATTACH
                                            : Enumerations.Action.WEAR,
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
                                    Replace = true,
                                    Slot = slot
                                }),
                            corradeConfiguration.MaximumNotificationThreads);
                    });

                    // Schedule a rebake.
                    RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                };
        }
    }
}