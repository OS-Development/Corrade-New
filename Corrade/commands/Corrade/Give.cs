///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using Corrade.Constants;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Timers;
using Inventory = wasOpenMetaverse.Inventory;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> give =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var item = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                    InventoryBase inventoryBase = null;
                    UUID itemUUID;
                    switch (UUID.TryParse(item, out itemUUID))
                    {
                        case true:
                            Locks.ClientInstanceInventoryLock.EnterReadLock();
                            if (Client.Inventory.Store.Contains(itemUUID))
                                inventoryBase = Client.Inventory.Store[itemUUID];
                            Locks.ClientInstanceInventoryLock.ExitReadLock();
                            break;

                        default:
                            inventoryBase = Inventory.FindInventory<InventoryBase>(Client, item,
                                CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                corradeConfiguration.ServicesTimeout);
                            break;
                    }
                    if (inventoryBase == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                    // Store the parent UUID for updates later on.
                    var parentUUID = UUID.Zero;
                    switch (inventoryBase.ParentUUID.Equals(UUID.Zero))
                    {
                        case true:
                            Locks.ClientInstanceInventoryLock.EnterReadLock();
                            var rootFolderUUID = Client.Inventory.Store.RootFolder.UUID;
                            var libraryFolderUUID = Client.Inventory.Store.LibraryFolder.UUID;
                            Locks.ClientInstanceInventoryLock.ExitReadLock();
                            if (inventoryBase.UUID.Equals(rootFolderUUID))
                            {
                                parentUUID = rootFolderUUID;
                                break;
                            }
                            if (inventoryBase.UUID.Equals(libraryFolderUUID))
                                parentUUID = libraryFolderUUID;
                            break;

                        default:
                            parentUUID = inventoryBase.ParentUUID;
                            break;
                    }
                    // If the requested item is an inventory item.
                    if (inventoryBase is InventoryItem)
                    {
                        // Sending an item requires transfer permission.
                        if (!(inventoryBase as InventoryItem).Permissions.OwnerMask.HasFlag(PermissionMask.Transfer))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_PERMISSIONS_FOR_ITEM);
                        // Set requested permissions if any on the item.
                        var permissions = wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PERMISSIONS)),
                                corradeCommandParameters.Message));
                        if (!string.IsNullOrEmpty(permissions))
                        {
                            (inventoryBase as InventoryItem).Permissions =
                                Inventory.wasStringToPermissions(permissions);
                            Client.Inventory.RequestUpdateItem(inventoryBase as InventoryItem);
                        }
                    }
                    // If the requested item is a folder.
                    else if (inventoryBase is InventoryFolder)
                    {
                        // Keep track of all inventory items found.
                        var items = new HashSet<InventoryBase>();
                        // Create the queue of folders.
                        var inventoryFolders = new BlockingQueue<InventoryFolder>();
                        // Enqueue the first folder (root).
                        inventoryFolders.Enqueue(inventoryBase as InventoryFolder);

                        InventoryFolder currentFolder = null;

                        var LockObject = new object();
                        var FolderUpdatedEvent = new ManualResetEventSlim(false);
                        EventHandler<FolderUpdatedEventArgs> FolderUpdatedEventHandler = (p, q) =>
                        {
                            if (!q.FolderID.Equals(currentFolder.UUID)) return;

                            var folderContents = Client.Inventory.Store.GetContents(q.FolderID);

                            lock (LockObject)
                            {
                                items.UnionWith(folderContents);
                            }

                            folderContents.AsParallel()
                                .Where(o => o is InventoryFolder)
                                .ForAll(o => inventoryFolders.Enqueue(o as InventoryFolder));
                            FolderUpdatedEvent.Set();
                        };

                        do
                        {
                            // Dequeue folder.
                            currentFolder = inventoryFolders.Dequeue();
                            Locks.ClientInstanceInventoryLock.EnterReadLock();
                            Client.Inventory.FolderUpdated += FolderUpdatedEventHandler;
                            FolderUpdatedEvent.Reset();
                            Client.Inventory.RequestFolderContents(currentFolder.UUID, currentFolder.OwnerID, true,
                                true,
                                InventorySortOrder.ByDate);
                            if (!FolderUpdatedEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                            {
                                Client.Inventory.FolderUpdated -= FolderUpdatedEventHandler;
                                Locks.ClientInstanceInventoryLock.ExitReadLock();
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_GETTING_FOLDER_CONTENTS);
                            }
                            Client.Inventory.FolderUpdated -= FolderUpdatedEventHandler;
                            Locks.ClientInstanceInventoryLock.ExitReadLock();
                        } while (inventoryFolders.Any());

                        // Check that if we are in SecondLife we would not transfer more items than SecondLife allows.
                        if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                            items.Count >
                            wasOpenMetaverse.Constants.INVENTORY.MAXIMUM_FOLDER_TRANSFER_ITEM_COUNT)
                            throw new Command.ScriptException(
                                Enumerations.ScriptError.TRANSFER_WOULD_EXCEED_MAXIMUM_COUNT);

                        // Check that all the items to be transferred have transfer permission.
                        if (items.AsParallel()
                            .Where(o => o is InventoryItem)
                            .Any(o => !(o as InventoryItem).Permissions.OwnerMask.HasFlag(PermissionMask.Transfer)))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_PERMISSIONS_FOR_ITEM);
                    }

                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Entity>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                    corradeCommandParameters.Message))))
                    {
                        case Enumerations.Entity.AVATAR:
                            UUID agentUUID;
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                            corradeCommandParameters.Message)), out agentUUID) &&
                                !Resolvers.AgentNameToUUID(Client,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(
                                                Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME)),
                                            corradeCommandParameters.Message)),
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                            corradeCommandParameters.Message)),
                                    corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout,
                                    new DecayingAlarm(corradeConfiguration.DataDecayType),
                                    ref agentUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                            if (inventoryBase is InventoryItem)
                            {
                                Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                Client.Inventory.GiveItem(inventoryBase.UUID, inventoryBase.Name,
                                    (inventoryBase as InventoryItem).AssetType, agentUUID, true);
                                Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                break;
                            }
                            if (inventoryBase is InventoryFolder)
                            {
                                Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                Client.Inventory.GiveFolder(inventoryBase.UUID, inventoryBase.Name,
                                    AssetType.Folder, agentUUID, true);
                                Locks.ClientInstanceInventoryLock.ExitWriteLock();
                            }
                            break;

                        case Enumerations.Entity.OBJECT:
                            // Cannot transfer folders to objects.
                            if (inventoryBase is InventoryFolder)
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ITEM_TYPE);
                            float range;
                            if (
                                !float.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                            corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                    out range))
                                range = corradeConfiguration.Range;
                            Primitive primitive = null;
                            var target = wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                                corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(target))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_TARGET_SPECIFIED);
                            UUID targetUUID;
                            if (UUID.TryParse(target, out targetUUID))
                            {
                                if (
                                    !Services.FindPrimitive(Client,
                                        targetUUID,
                                        range,
                                        ref primitive,
                                        corradeConfiguration.DataTimeout))
                                    throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                            }
                            else
                            {
                                if (
                                    !Services.FindPrimitive(Client,
                                        target,
                                        range,
                                        ref primitive,
                                        corradeConfiguration.DataTimeout))
                                    throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                            }
                            Locks.ClientInstanceInventoryLock.EnterWriteLock();
                            Client.Inventory.UpdateTaskInventory(primitive.LocalID, inventoryBase as InventoryItem);
                            Locks.ClientInstanceInventoryLock.ExitWriteLock();
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }

                    // Mark parent folder as needing an update.
                    if (!parentUUID.Equals(UUID.Zero))
                    {
                        Locks.ClientInstanceInventoryLock.EnterReadLock();
                        Client.Inventory.Store.GetNodeFor(parentUUID).NeedsUpdate = true;
                        Locks.ClientInstanceInventoryLock.ExitReadLock();
                    }
                };
        }
    }
}