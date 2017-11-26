///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Constants;
using Corrade.Structures;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> inventory =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                    lock (GroupDirectoryTrackersLock)
                    {
                        if (!GroupDirectoryTrackers.ContainsKey(corradeCommandParameters.Group.UUID))
                            GroupDirectoryTrackers.Add(corradeCommandParameters.Group.UUID,
                                Client.Inventory.Store.RootFolder);
                    }
                    var path =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PATH)),
                            corradeCommandParameters.Message));
                    InventoryBase item = null;
                    var csv = new List<string>();
                    var action = Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                    );
                    var updateFolders = new HashSet<UUID>();
                    switch (action)
                    {
                        case Enumerations.Action.LS:
                            if (string.IsNullOrEmpty(path))
                                lock (GroupDirectoryTrackersLock)
                                {
                                    item =
                                        GroupDirectoryTrackers[corradeCommandParameters.Group.UUID];
                                }
                            item = Inventory.FindInventory<InventoryBase>(Client, path,
                                CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                corradeConfiguration.ServicesTimeout, item as InventoryFolder);
                            if (item == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.PATH_NOT_FOUND);
                            switch (item is InventoryFolder)
                            {
                                case true:
                                    var dirItems = new List<DirItem>();
                                    Locks.ClientInstanceInventoryLock.EnterReadLock();
                                    dirItems.AddRange(Client.Inventory.Store.GetContents(
                                        item.UUID).AsParallel().Select(o =>
                                        DirItem.FromInventoryBase(Client, o,
                                            corradeConfiguration.ServicesTimeout)));
                                    Locks.ClientInstanceInventoryLock.ExitReadLock();
                                    foreach (var dirItem in dirItems.OrderByDescending(o => o.Time))
                                    {
                                        csv.AddRange(new[]
                                            {Reflection.GetStructureMemberName(dirItem, dirItem.Name), dirItem.Name});
                                        csv.AddRange(new[]
                                        {
                                            Reflection.GetStructureMemberName(dirItem, dirItem.Item),
                                            dirItem.Item.ToString()
                                        });
                                        csv.AddRange(new[]
                                        {
                                            Reflection.GetStructureMemberName(dirItem, dirItem.Type),
                                            Reflection.GetNameFromEnumValue(dirItem.Type)
                                        });
                                        csv.AddRange(new[]
                                        {
                                            Reflection.GetStructureMemberName(dirItem, dirItem.Permissions),
                                            dirItem.Permissions
                                        });
                                        csv.AddRange(new[]
                                        {
                                            Reflection.GetStructureMemberName(dirItem, dirItem.Time),
                                            dirItem.Time.ToString(wasOpenMetaverse.Constants.LSL.DATE_TIME_STAMP)
                                        });
                                    }
                                    break;

                                case false:
                                    var dir = DirItem.FromInventoryBase(Client, item,
                                        corradeConfiguration.ServicesTimeout);
                                    csv.AddRange(new[] {Reflection.GetStructureMemberName(dir, dir.Name), dir.Name});
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetStructureMemberName(dir, dir.Item),
                                        dir.Item.ToString()
                                    });
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetStructureMemberName(dir, dir.Type),
                                        Reflection.GetNameFromEnumValue(dir.Type)
                                    });
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetStructureMemberName(dir, dir.Permissions),
                                        dir.Permissions
                                    });
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetStructureMemberName(dir, dir.Time),
                                        dir.Time.ToString(wasOpenMetaverse.Constants.LSL.DATE_TIME_STAMP)
                                    });
                                    break;
                            }
                            break;

                        case Enumerations.Action.CWD:
                            lock (GroupDirectoryTrackersLock)
                            {
                                var dirItem =
                                    DirItem.FromInventoryBase(Client,
                                        GroupDirectoryTrackers[corradeCommandParameters.Group.UUID],
                                        corradeConfiguration.ServicesTimeout);
                                csv.AddRange(new[]
                                    {Reflection.GetStructureMemberName(dirItem, dirItem.Name), dirItem.Name});
                                csv.AddRange(new[]
                                {
                                    Reflection.GetStructureMemberName(dirItem, dirItem.Item), dirItem.Item.ToString()
                                });
                                csv.AddRange(new[]
                                {
                                    Reflection.GetStructureMemberName(dirItem, dirItem.Type),
                                    Reflection.GetNameFromEnumValue(dirItem.Type)
                                });
                                csv.AddRange(new[]
                                {
                                    Reflection.GetStructureMemberName(dirItem, dirItem.Permissions),
                                    dirItem.Permissions
                                });
                                csv.AddRange(new[]
                                {
                                    Reflection.GetStructureMemberName(dirItem, dirItem.Time),
                                    dirItem.Time.ToString(wasOpenMetaverse.Constants.LSL.DATE_TIME_STAMP)
                                });
                            }
                            break;

                        case Enumerations.Action.CD:
                            if (string.IsNullOrEmpty(path))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_PATH_PROVIDED);
                            if (!path[0].Equals(CORRADE_CONSTANTS.PATH_SEPARATOR))
                                lock (GroupDirectoryTrackersLock)
                                {
                                    item =
                                        GroupDirectoryTrackers[corradeCommandParameters.Group.UUID];
                                }
                            item = Inventory.FindInventory<InventoryBase>(Client, path,
                                CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                corradeConfiguration.ServicesTimeout, item as InventoryFolder);
                            if (item == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.PATH_NOT_FOUND);
                            if (!(item is InventoryFolder))
                                throw new Command.ScriptException(Enumerations.ScriptError.UNEXPECTED_ITEM_IN_PATH);
                            lock (GroupDirectoryTrackersLock)
                            {
                                GroupDirectoryTrackers[corradeCommandParameters.Group.UUID] = item;
                            }
                            break;

                        case Enumerations.Action.MKDIR:
                            var mkdirName =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(mkdirName))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);
                            if (string.IsNullOrEmpty(path))
                                lock (GroupDirectoryTrackersLock)
                                {
                                    item =
                                        GroupDirectoryTrackers[corradeCommandParameters.Group.UUID];
                                }
                            item = Inventory.FindInventory<InventoryBase>(Client, path,
                                CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                corradeConfiguration.ServicesTimeout, item as InventoryFolder);
                            if (item == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.PATH_NOT_FOUND);
                            if (!(item is InventoryFolder))
                                throw new Command.ScriptException(Enumerations.ScriptError.UNEXPECTED_ITEM_IN_PATH);
                            if (Client.Inventory.CreateFolder(item.UUID, mkdirName) == UUID.Zero)
                                throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_CREATE_FOLDER);
                            if (!updateFolders.Contains(item.UUID))
                                updateFolders.Add(item.UUID);
                            break;

                        case Enumerations.Action.CHMOD:
                            var itemPermissions =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PERMISSIONS)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(itemPermissions))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_PERMISSIONS_PROVIDED);
                            if (string.IsNullOrEmpty(path))
                                lock (GroupDirectoryTrackersLock)
                                {
                                    item =
                                        GroupDirectoryTrackers[corradeCommandParameters.Group.UUID];
                                }
                            item = Inventory.FindInventory<InventoryBase>(Client, path,
                                CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                corradeConfiguration.ServicesTimeout, item as InventoryFolder);
                            if (item == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.PATH_NOT_FOUND);
                            switch (item is InventoryFolder)
                            {
                                case true:
                                    Locks.ClientInstanceInventoryLock.EnterReadLock();
                                    Client.Inventory.Store.GetContents(item.UUID)
                                        .OfType<InventoryItem>()
                                        .AsParallel()
                                        .ForAll(o =>
                                        {
                                            Inventory.wasStringToPermissions(itemPermissions, out o.Permissions);
                                            Client.Inventory.RequestUpdateItem(o);
                                        });
                                    Locks.ClientInstanceInventoryLock.ExitReadLock();
                                    if (!updateFolders.Contains(item.UUID))
                                        updateFolders.Add(item.UUID);
                                    break;

                                default:
                                    var inventoryItem = item as InventoryItem;
                                    Inventory.wasStringToPermissions(itemPermissions, out inventoryItem.Permissions);
                                    Client.Inventory.RequestUpdateItem(item as InventoryItem);
                                    if (!updateFolders.Contains(inventoryItem.ParentUUID))
                                        updateFolders.Add(inventoryItem.ParentUUID);
                                    break;
                            }
                            break;

                        case Enumerations.Action.RM:
                            if (string.IsNullOrEmpty(path))
                                lock (GroupDirectoryTrackersLock)
                                {
                                    item =
                                        GroupDirectoryTrackers[corradeCommandParameters.Group.UUID];
                                }
                            item = Inventory.FindInventory<InventoryBase>(Client, path,
                                CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                corradeConfiguration.ServicesTimeout, item as InventoryFolder);
                            if (item == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.PATH_NOT_FOUND);
                            var parentUUID = item.ParentUUID;
                            if (!updateFolders.Contains(parentUUID))
                                updateFolders.Add(parentUUID);
                            switch (item is InventoryFolder)
                            {
                                case true:
                                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                    Client.Inventory.MoveFolder(item.UUID,
                                        Client.Inventory.FindFolderForType(AssetType.TrashFolder));
                                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                    break;

                                default:
                                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                    Client.Inventory.MoveItem(item.UUID,
                                        Client.Inventory.FindFolderForType(AssetType.TrashFolder));
                                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                    break;
                            }
                            Locks.ClientInstanceInventoryLock.EnterReadLock();
                            var trashFolderUUID = Client.Inventory.FindFolderForType(AssetType.TrashFolder);
                            Locks.ClientInstanceInventoryLock.ExitReadLock();
                            if (!updateFolders.Contains(trashFolderUUID))
                                updateFolders.Add(trashFolderUUID);
                            break;

                        case Enumerations.Action.CP:
                        case Enumerations.Action.MV:
                        case Enumerations.Action.LN:
                            var sourcePath =
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SOURCE)),
                                    corradeCommandParameters.Message));
                            InventoryBase sourceItem = null;
                            if (string.IsNullOrEmpty(sourcePath))
                                lock (GroupDirectoryTrackersLock)
                                {
                                    sourceItem =
                                        GroupDirectoryTrackers[corradeCommandParameters.Group.UUID];
                                }

                            sourceItem = Inventory.FindInventory<InventoryBase>(Client, sourcePath,
                                CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                corradeConfiguration.ServicesTimeout, sourceItem as InventoryFolder);
                            if (sourceItem == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.PATH_NOT_FOUND);

                            switch (action)
                            {
                                case Enumerations.Action.CP:
                                case Enumerations.Action.LN:
                                    if (sourceItem is InventoryFolder)
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.EXPECTED_ITEM_AS_SOURCE);
                                    break;
                            }
                            var targetPath =
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                                    corradeCommandParameters.Message));
                            InventoryBase targetItem = null;
                            if (string.IsNullOrEmpty(targetPath))
                                lock (GroupDirectoryTrackersLock)
                                {
                                    targetItem =
                                        GroupDirectoryTrackers[corradeCommandParameters.Group.UUID];
                                }
                            var targetName = sourceItem.Name;
                            var targetFolder = targetItem as InventoryFolder;
                            targetItem = Inventory.FindInventory<InventoryBase>(Client, targetPath,
                                CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                corradeConfiguration.ServicesTimeout, targetFolder);
                            if (!(targetItem is InventoryFolder))
                            {
                                var targetSegments =
                                    new List<string>(targetPath.PathSplit(CORRADE_CONSTANTS.PATH_SEPARATOR,
                                        CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE, false));

                                targetItem = Inventory.FindInventory<InventoryBase>(Client,
                                    string.Join(CORRADE_CONSTANTS.PATH_SEPARATOR.ToString(),
                                        targetSegments.Take(targetSegments.Count - 1)
                                            .Select(
                                                o =>
                                                    string.Join(
                                                        CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE.ToString() +
                                                        CORRADE_CONSTANTS.PATH_SEPARATOR.ToString(),
                                                        o.Split(CORRADE_CONSTANTS.PATH_SEPARATOR)))),
                                    CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                    corradeConfiguration.ServicesTimeout, targetFolder);

                                if (!(targetItem is InventoryFolder))
                                    throw new Command.ScriptException(Enumerations.ScriptError.PATH_NOT_FOUND);

                                targetName = targetSegments.Last();
                            }
                            switch (action)
                            {
                                case Enumerations.Action.LN:
                                    var sourceInventoryItem = sourceItem as InventoryItem;
                                    if (sourceInventoryItem == null)
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.EXPECTED_ITEM_AS_SOURCE);
                                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                    Client.Inventory.CreateLink(targetItem.UUID, sourceItem.UUID, targetName,
                                        sourceInventoryItem.Description, sourceInventoryItem.AssetType,
                                        sourceInventoryItem.InventoryType, UUID.Random(), (succeeded, newItem) =>
                                        {
                                            if (!succeeded)
                                                throw new Command.ScriptException(
                                                    Enumerations.ScriptError.UNABLE_TO_CREATE_ITEM);
                                            Client.Inventory.RequestFetchInventory(newItem.UUID, newItem.OwnerID);
                                            if (!updateFolders.Contains(newItem.ParentUUID))
                                                updateFolders.Add(newItem.ParentUUID);
                                        });
                                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                    break;

                                case Enumerations.Action.MV:
                                    switch (sourceItem is InventoryFolder)
                                    {
                                        case true:
                                            Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                            Client.Inventory.MoveFolder(sourceItem.UUID, targetItem.UUID, targetName);
                                            Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                            break;

                                        default:
                                            Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                            Client.Inventory.MoveItem(sourceItem.UUID, targetItem.UUID, targetName);
                                            Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                            break;
                                    }
                                    var rootFolderUUID = Client.Inventory.Store.RootFolder.UUID;
                                    var libraryFolderUUID = Client.Inventory.Store.LibraryFolder.UUID;
                                    switch (sourceItem.ParentUUID.Equals(UUID.Zero))
                                    {
                                        case true:

                                            if (sourceItem.UUID.Equals(rootFolderUUID) &&
                                                !updateFolders.Contains(rootFolderUUID))
                                            {
                                                updateFolders.Add(rootFolderUUID);
                                                break;
                                            }
                                            if (sourceItem.UUID.Equals(libraryFolderUUID) &&
                                                !updateFolders.Contains(rootFolderUUID))
                                                updateFolders.Add(libraryFolderUUID);
                                            break;

                                        default:
                                            if (!updateFolders.Contains(sourceItem.ParentUUID))
                                                updateFolders.Add(sourceItem.ParentUUID);
                                            break;
                                    }
                                    switch (targetItem.ParentUUID.Equals(UUID.Zero))
                                    {
                                        case true:

                                            if (targetItem.UUID.Equals(rootFolderUUID) &&
                                                !updateFolders.Contains(rootFolderUUID))
                                            {
                                                updateFolders.Add(rootFolderUUID);
                                                break;
                                            }
                                            if (targetItem.UUID.Equals(libraryFolderUUID) &&
                                                !updateFolders.Contains(rootFolderUUID))
                                                updateFolders.Add(libraryFolderUUID);
                                            break;

                                        default:
                                            if (!updateFolders.Contains(targetItem.ParentUUID))
                                                updateFolders.Add(targetItem.ParentUUID);
                                            break;
                                    }
                                    break;

                                case Enumerations.Action.CP:
                                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                    Client.Inventory.RequestCopyItem(sourceItem.UUID, targetItem.UUID,
                                        targetName,
                                        newItem =>
                                        {
                                            if (newItem == null)
                                                throw new Command.ScriptException(
                                                    Enumerations.ScriptError.UNABLE_TO_CREATE_ITEM);
                                            Client.Inventory.RequestFetchInventory(newItem.UUID, newItem.OwnerID);
                                            if (!updateFolders.Contains(newItem.ParentUUID))
                                                updateFolders.Add(newItem.ParentUUID);
                                        });
                                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                    break;
                            }
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                    // Mark all folders to be updated as needing an update.
                    if (updateFolders.Any())
                    {
                        Locks.ClientInstanceInventoryLock.EnterWriteLock();
                        updateFolders.AsParallel()
                            .Select(o => Client.Inventory.Store.GetNodeFor(o))
                            .Where(o => o != null)
                            .ForAll(o => o.NeedsUpdate = true);
                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                    }
                    if (csv.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                };
        }
    }
}