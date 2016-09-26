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
            public static Action<Command.CorradeCommandParameters, Dictionary<string, string>> inventory =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }

                    lock (GroupDirectoryTrackersLock)
                    {
                        if (!GroupDirectoryTrackers.ContainsKey(corradeCommandParameters.Group.UUID))
                        {
                            GroupDirectoryTrackers.Add(corradeCommandParameters.Group.UUID,
                                Client.Inventory.Store.RootFolder);
                        }
                    }
                    var path =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PATH)),
                            corradeCommandParameters.Message));
                    Func<string, InventoryBase, InventoryBase> findPath = null;
                    findPath = (o, p) =>
                    {
                        if (string.IsNullOrEmpty(o)) return p;

                        // Split all paths.
                        var unpack = o.Split(CORRADE_CONSTANTS.PATH_SEPARATOR[0]);
                        // Pop first item to process.
                        var first = unpack.First();
                        // Remove first item.
                        unpack = unpack.Skip(1).ToArray();

                        var next = p;

                        // Avoid preceeding slashes.
                        if (string.IsNullOrEmpty(first)) goto CONTINUE;

                        var contents = new HashSet<InventoryBase>();
                        lock (Locks.ClientInstanceInventoryLock)
                        {
                            contents.UnionWith(Client.Inventory.Store.GetContents(p.UUID));
                        }
                        try
                        {
                            UUID itemUUID;
                            switch (!UUID.TryParse(first, out itemUUID))
                            {
                                case true:
                                    next = contents.SingleOrDefault(q => q.Name.Equals(first));
                                    break;
                                default:
                                    next = contents.SingleOrDefault(q => q.UUID.Equals(itemUUID));
                                    break;
                            }
                        }
                        catch (Exception)
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.AMBIGUOUS_PATH);
                        }

                        switch (next != null && !next.Equals(default(InventoryBase)))
                        {
                            case false:
                                return null;
                        }

                        if (next is InventoryItem)
                        {
                            return next;
                        }

                        CONTINUE:
                        return findPath(string.Join(CORRADE_CONSTANTS.PATH_SEPARATOR, unpack),
                            Client.Inventory.Store[next.UUID]);
                    };
                    InventoryBase item;
                    var csv = new List<string>();
                    var action = Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant());
                    switch (action)
                    {
                        case Enumerations.Action.LS:
                            switch (!string.IsNullOrEmpty(path))
                            {
                                case true:
                                    if (path[0].Equals(CORRADE_CONSTANTS.PATH_SEPARATOR[0]))
                                    {
                                        lock (Locks.ClientInstanceInventoryLock)
                                        {
                                            item = Client.Inventory.Store.RootFolder;
                                        }
                                        break;
                                    }
                                    goto default;
                                default:
                                    lock (GroupDirectoryTrackersLock)
                                    {
                                        item =
                                            GroupDirectoryTrackers[corradeCommandParameters.Group.UUID];
                                    }
                                    break;
                            }
                            item = findPath(path, item);
                            if (item == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.PATH_NOT_FOUND);
                            switch (item is InventoryFolder)
                            {
                                case true:
                                    var dirItems = new List<DirItem>();
                                    lock (Locks.ClientInstanceInventoryLock)
                                    {
                                        dirItems.AddRange(Client.Inventory.Store.GetContents(
                                            item.UUID).AsParallel().Select(o =>
                                                DirItem.FromInventoryBase(Client, o,
                                                    corradeConfiguration.ServicesTimeout)));
                                    }
                                    foreach (var dirItem in dirItems)
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
                                {Reflection.GetStructureMemberName(dirItem, dirItem.Item), dirItem.Item.ToString()});
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
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_PATH_PROVIDED);
                            }
                            switch (!path[0].Equals(CORRADE_CONSTANTS.PATH_SEPARATOR[0]))
                            {
                                case true:
                                    lock (GroupDirectoryTrackersLock)
                                    {
                                        item =
                                            GroupDirectoryTrackers[corradeCommandParameters.Group.UUID];
                                    }
                                    break;
                                default:
                                    lock (Locks.ClientInstanceInventoryLock)
                                    {
                                        item = Client.Inventory.Store.RootFolder;
                                    }
                                    break;
                            }
                            item = findPath(path, item);
                            if (item == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.PATH_NOT_FOUND);
                            if (!(item is InventoryFolder))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.UNEXPECTED_ITEM_IN_PATH);
                            }
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
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);
                            }
                            switch (!string.IsNullOrEmpty(path))
                            {
                                case true:
                                    if (path[0].Equals(CORRADE_CONSTANTS.PATH_SEPARATOR[0]))
                                    {
                                        lock (Locks.ClientInstanceInventoryLock)
                                        {
                                            item = Client.Inventory.Store.RootFolder;
                                        }
                                        break;
                                    }
                                    goto default;
                                default:
                                    lock (GroupDirectoryTrackersLock)
                                    {
                                        item =
                                            GroupDirectoryTrackers[corradeCommandParameters.Group.UUID];
                                    }
                                    break;
                            }
                            item = findPath(path, item);
                            if (item == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.PATH_NOT_FOUND);
                            if (!(item is InventoryFolder))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.UNEXPECTED_ITEM_IN_PATH);
                            }
                            if (Client.Inventory.CreateFolder(item.UUID, mkdirName) == UUID.Zero)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_CREATE_FOLDER);
                            }
                            try
                            {
                                Inventory.UpdateInventoryRecursive(Client, Client.Inventory.Store[item.ParentUUID] as InventoryFolder,
                                    corradeConfiguration.ServicesTimeout);
                            }
                            catch (Exception)
                            {
                                Feedback(
                                    Reflection.GetDescriptionFromEnumValue(
                                        Enumerations.ConsoleMessage.ERROR_UPDATING_INVENTORY));
                            }
                            break;
                        case Enumerations.Action.CHMOD:
                            var itemPermissions =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PERMISSIONS)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(itemPermissions))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_PERMISSIONS_PROVIDED);
                            }
                            switch (!string.IsNullOrEmpty(path))
                            {
                                case true:
                                    if (path[0].Equals(CORRADE_CONSTANTS.PATH_SEPARATOR[0]))
                                    {
                                        lock (Locks.ClientInstanceInventoryLock)
                                        {
                                            item = Client.Inventory.Store.RootFolder;
                                        }
                                        break;
                                    }
                                    goto default;
                                default:
                                    lock (GroupDirectoryTrackersLock)
                                    {
                                        item =
                                            GroupDirectoryTrackers[corradeCommandParameters.Group.UUID];
                                    }
                                    break;
                            }
                            item = findPath(path, item);
                            if (item == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.PATH_NOT_FOUND);
                            switch (item is InventoryFolder)
                            {
                                case true:
                                    lock (Locks.ClientInstanceInventoryLock)
                                    {
                                        /*if (Client.Inventory.Store.GetContents(
                                            item.UUID)
                                            .OfType<InventoryItem>()
                                            .Any(
                                                inventoryItem =>
                                                    !Inventory.wasSetInventoryItemPermissions(Client, inventoryItem,
                                                        itemPermissions, corradeConfiguration.ServicesTimeout)))
                                        {
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.SETTING_PERMISSIONS_FAILED);
                                        }*/
                                        Client.Inventory.Store.GetContents(item.UUID).OfType<InventoryItem>().AsParallel().ForAll(o =>
                                        {
                                            o.Permissions = Inventory.wasStringToPermissions(itemPermissions);
                                            Client.Inventory.RequestUpdateItem(o);
                                        });
                                    }
                                    break;
                                default:
                                    /*if (
                                        !Inventory.wasSetInventoryItemPermissions(Client, item as InventoryItem,
                                            itemPermissions, corradeConfiguration.ServicesTimeout))
                                    {
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.SETTING_PERMISSIONS_FAILED);
                                    }*/
                                    (item as InventoryItem).Permissions = Inventory.wasStringToPermissions(itemPermissions);
                                    Client.Inventory.RequestUpdateItem(item as InventoryItem);
                                    break;
                            }
                            try
                            {
                                Inventory.UpdateInventoryRecursive(Client, Client.Inventory.Store[item.ParentUUID] as InventoryFolder, 
                                    corradeConfiguration.ServicesTimeout);
                            }
                            catch (Exception)
                            {
                                Feedback(
                                    Reflection.GetDescriptionFromEnumValue(
                                        Enumerations.ConsoleMessage.ERROR_UPDATING_INVENTORY));
                            }
                            break;
                        case Enumerations.Action.RM:
                            switch (!string.IsNullOrEmpty(path))
                            {
                                case true:
                                    if (path[0].Equals(CORRADE_CONSTANTS.PATH_SEPARATOR[0]))
                                    {
                                        lock (Locks.ClientInstanceInventoryLock)
                                        {
                                            item = Client.Inventory.Store.RootFolder;
                                        }
                                        break;
                                    }
                                    goto default;
                                default:
                                    lock (GroupDirectoryTrackersLock)
                                    {
                                        item =
                                            GroupDirectoryTrackers[corradeCommandParameters.Group.UUID];
                                    }
                                    break;
                            }
                            item = findPath(path, item);
                            if (item == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.PATH_NOT_FOUND);
                            switch (item is InventoryFolder)
                            {
                                case true:
                                    lock (Locks.ClientInstanceInventoryLock)
                                    {
                                        Client.Inventory.MoveFolder(item.UUID,
                                            Client.Inventory.FindFolderForType(AssetType.TrashFolder));
                                    }
                                    break;
                                default:
                                    lock (Locks.ClientInstanceInventoryLock)
                                    {
                                        Client.Inventory.MoveItem(item.UUID,
                                            Client.Inventory.FindFolderForType(AssetType.TrashFolder));
                                    }
                                    break;
                            }
                            try
                            {
                                Inventory.UpdateInventoryRecursive(Client, Client.Inventory.Store[item.ParentUUID] as InventoryFolder,
                                    corradeConfiguration.ServicesTimeout);
                            }
                            catch (Exception)
                            {
                                Feedback(
                                    Reflection.GetDescriptionFromEnumValue(
                                        Enumerations.ConsoleMessage.ERROR_UPDATING_INVENTORY));
                            }
                            break;
                        case Enumerations.Action.CP:
                        case Enumerations.Action.MV:
                        case Enumerations.Action.LN:
                            var lnSourcePath =
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SOURCE)),
                                    corradeCommandParameters.Message));
                            InventoryBase sourceItem;
                            switch (!string.IsNullOrEmpty(lnSourcePath))
                            {
                                case true:
                                    if (lnSourcePath[0].Equals(CORRADE_CONSTANTS.PATH_SEPARATOR[0]))
                                    {
                                        sourceItem = Client.Inventory.Store.RootFolder;
                                        break;
                                    }
                                    goto default;
                                default:
                                    lock (GroupDirectoryTrackersLock)
                                    {
                                        sourceItem =
                                            GroupDirectoryTrackers[corradeCommandParameters.Group.UUID];
                                    }
                                    break;
                            }
                            sourceItem = findPath(lnSourcePath, sourceItem);
                            if (sourceItem == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.PATH_NOT_FOUND);
                            var sourceSegments = lnSourcePath.Split(CORRADE_CONSTANTS.PATH_SEPARATOR[0]);
                            var parentSource = findPath(string.Join(CORRADE_CONSTANTS.PATH_SEPARATOR,
                                sourceSegments.Take(sourceSegments.Length - 1)), sourceItem);
                            switch (action)
                            {
                                case Enumerations.Action.CP:
                                case Enumerations.Action.LN:
                                    if (sourceItem is InventoryFolder)
                                    {
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.EXPECTED_ITEM_AS_SOURCE);
                                    }
                                    break;
                            }
                            var lnTargetPath =
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                                    corradeCommandParameters.Message));
                            InventoryBase targetItem;
                            switch (!string.IsNullOrEmpty(lnTargetPath))
                            {
                                case true:
                                    if (lnTargetPath[0].Equals(CORRADE_CONSTANTS.PATH_SEPARATOR[0]))
                                    {
                                        targetItem = Client.Inventory.Store.RootFolder;
                                        break;
                                    }
                                    goto default;
                                default:
                                    lock (GroupDirectoryTrackersLock)
                                    {
                                        targetItem =
                                            GroupDirectoryTrackers[corradeCommandParameters.Group.UUID];
                                    }
                                    break;
                            }
                            var targetName = sourceItem.Name;
                            var parentTarget = findPath(lnTargetPath, targetItem);
                            switch (parentTarget is InventoryFolder)
                            {
                                case false:
                                    var pathSegments = lnTargetPath.Split(CORRADE_CONSTANTS.PATH_SEPARATOR[0]);
                                    parentTarget = findPath(string.Join(CORRADE_CONSTANTS.PATH_SEPARATOR,
                                        pathSegments.Take(pathSegments.Length - 1)), targetItem);
                                    if (!(parentTarget is InventoryFolder))
                                        throw new Command.ScriptException(Enumerations.ScriptError.PATH_NOT_FOUND);
                                    targetName = pathSegments.Last();
                                    break;
                                default:
                                    targetItem = parentTarget;
                                    break;
                            }
                            switch (action)
                            {
                                case Enumerations.Action.LN:
                                    var sourceInventoryItem = sourceItem as InventoryItem;
                                    if (sourceInventoryItem == null)
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.EXPECTED_ITEM_AS_SOURCE);
                                    lock (Locks.ClientInstanceInventoryLock)
                                    {
                                        Client.Inventory.CreateLink(targetItem.UUID, sourceItem.UUID, targetName,
                                            sourceInventoryItem.Description, sourceInventoryItem.AssetType,
                                            sourceInventoryItem.InventoryType, UUID.Random(), (succeeded, newItem) =>
                                            {
                                                if (!succeeded)
                                                {
                                                    throw new Command.ScriptException(
                                                        Enumerations.ScriptError.UNABLE_TO_CREATE_ITEM);
                                                }
                                                Client.Inventory.RequestFetchInventory(newItem.UUID, newItem.OwnerID);
                                            });
                                    }
                                    break;
                                case Enumerations.Action.MV:
                                    switch (sourceItem is InventoryFolder)
                                    {
                                        case true:
                                            lock (Locks.ClientInstanceInventoryLock)
                                            {
                                                Client.Inventory.MoveFolder(sourceItem.UUID, targetItem.UUID, targetName);
                                            }
                                            break;
                                        default:
                                            lock (Locks.ClientInstanceInventoryLock)
                                            {
                                                Client.Inventory.MoveItem(sourceItem.UUID, targetItem.UUID, targetName);
                                            }
                                            break;
                                    }
                                    if (parentSource is InventoryFolder)
                                    {
                                        Inventory.UpdateInventoryRecursive(Client, parentSource as InventoryFolder,
                                            corradeConfiguration.ServicesTimeout);
                                    }
                                    if (parentTarget is InventoryFolder)
                                    {
                                        Inventory.UpdateInventoryRecursive(Client, parentTarget as InventoryFolder,
                                            corradeConfiguration.ServicesTimeout);
                                    }
                                    break;
                                case Enumerations.Action.CP:
                                    lock (Locks.ClientInstanceInventoryLock)
                                    {
                                        Client.Inventory.RequestCopyItem(sourceItem.UUID, targetItem.UUID,
                                            targetName,
                                            newItem =>
                                            {
                                                if (newItem == null)
                                                {
                                                    throw new Command.ScriptException(
                                                        Enumerations.ScriptError.UNABLE_TO_CREATE_ITEM);
                                                }
                                                Client.Inventory.RequestFetchInventory(newItem.UUID, newItem.OwnerID);
                                            });
                                    }
                                    break;
                            }
                            try
                            {
                                Inventory.UpdateInventoryRecursive(Client, Client.Inventory.Store.RootFolder,
                                    corradeConfiguration.ServicesTimeout);
                            }
                            catch (Exception)
                            {
                                Feedback(
                                    Reflection.GetDescriptionFromEnumValue(
                                        Enumerations.ConsoleMessage.ERROR_UPDATING_INVENTORY));
                            }
                            break;
                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                    if (csv.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                    }
                };
        }
    }
}