///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> inventory =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Inventory))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }

                    lock (GroupDirectoryTrackersLock)
                    {
                        if (!GroupDirectoryTrackers.Contains(corradeCommandParameters.Group.UUID))
                        {
                            GroupDirectoryTrackers.Add(corradeCommandParameters.Group.UUID,
                                Client.Inventory.Store.RootFolder);
                        }
                    }
                    string path =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.PATH)),
                            corradeCommandParameters.Message));
                    Func<string, InventoryBase, InventoryBase> findPath = null;
                    findPath = (o, p) =>
                    {
                        if (string.IsNullOrEmpty(o)) return p;

                        // Split all paths.
                        string[] unpack = o.Split(CORRADE_CONSTANTS.PATH_SEPARATOR[0]);
                        // Pop first item to process.
                        string first = unpack.First();
                        // Remove item.
                        unpack = unpack.AsParallel().Where(q => !q.Equals(first)).ToArray();

                        InventoryBase next = p;

                        // Avoid preceeding slashes.
                        if (string.IsNullOrEmpty(first)) goto CONTINUE;

                        HashSet<InventoryBase> contents =
                            new HashSet<InventoryBase>(Client.Inventory.Store.GetContents(p.UUID));
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
                            throw new ScriptException(ScriptError.AMBIGUOUS_PATH);
                        }

                        switch (next != null && !next.Equals(default(InventoryBase)))
                        {
                            case false:
                                throw new ScriptException(ScriptError.PATH_NOT_FOUND);
                        }

                        if (!(next is InventoryFolder))
                        {
                            return next;
                        }

                        CONTINUE:
                        return findPath(string.Join(CORRADE_CONSTANTS.PATH_SEPARATOR, unpack),
                            Client.Inventory.Store[next.UUID]);
                    };
                    InventoryBase item;
                    List<string> csv = new List<string>();
                    Action action = wasGetEnumValueFromDescription<Action>(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant());
                    switch (action)
                    {
                        case Action.LS:
                            switch (!string.IsNullOrEmpty(path))
                            {
                                case true:
                                    if (path[0].Equals(CORRADE_CONSTANTS.PATH_SEPARATOR[0]))
                                    {
                                        item = Client.Inventory.Store.RootFolder;
                                        break;
                                    }
                                    goto default;
                                default:
                                    lock (GroupDirectoryTrackersLock)
                                    {
                                        item =
                                            GroupDirectoryTrackers[corradeCommandParameters.Group.UUID] as InventoryBase;
                                    }
                                    break;
                            }
                            item = findPath(path, item);
                            switch (item is InventoryFolder)
                            {
                                case true:
                                    foreach (DirItem dirItem in Client.Inventory.Store.GetContents(
                                        item.UUID).AsParallel().Select(
                                            o => DirItem.FromInventoryBase(o)))
                                    {
                                        csv.AddRange(new[]
                                        {wasGetStructureMemberDescription(dirItem, dirItem.Name), dirItem.Name});
                                        csv.AddRange(new[]
                                        {
                                            wasGetStructureMemberDescription(dirItem, dirItem.Item),
                                            dirItem.Item.ToString()
                                        });
                                        csv.AddRange(new[]
                                        {
                                            wasGetStructureMemberDescription(dirItem, dirItem.Type),
                                            wasGetDescriptionFromEnumValue(dirItem.Type)
                                        });
                                        csv.AddRange(new[]
                                        {
                                            wasGetStructureMemberDescription(dirItem, dirItem.Permissions),
                                            dirItem.Permissions
                                        });
                                    }
                                    break;
                                case false:
                                    DirItem dir = DirItem.FromInventoryBase(item);
                                    csv.AddRange(new[] {wasGetStructureMemberDescription(dir, dir.Name), dir.Name});
                                    csv.AddRange(new[]
                                    {
                                        wasGetStructureMemberDescription(dir, dir.Item),
                                        dir.Item.ToString()
                                    });
                                    csv.AddRange(new[]
                                    {
                                        wasGetStructureMemberDescription(dir, dir.Type),
                                        wasGetDescriptionFromEnumValue(dir.Type)
                                    });
                                    csv.AddRange(new[]
                                    {
                                        wasGetStructureMemberDescription(dir, dir.Permissions),
                                        dir.Permissions
                                    });
                                    break;
                            }
                            break;
                        case Action.CWD:
                            lock (GroupDirectoryTrackersLock)
                            {
                                DirItem dirItem =
                                    DirItem.FromInventoryBase(
                                        GroupDirectoryTrackers[corradeCommandParameters.Group.UUID] as InventoryBase);
                                csv.AddRange(new[]
                                {wasGetStructureMemberDescription(dirItem, dirItem.Name), dirItem.Name});
                                csv.AddRange(new[]
                                {wasGetStructureMemberDescription(dirItem, dirItem.Item), dirItem.Item.ToString()});
                                csv.AddRange(new[]
                                {
                                    wasGetStructureMemberDescription(dirItem, dirItem.Type),
                                    wasGetDescriptionFromEnumValue(dirItem.Type)
                                });
                                csv.AddRange(new[]
                                {
                                    wasGetStructureMemberDescription(dirItem, dirItem.Permissions),
                                    dirItem.Permissions
                                });
                            }
                            break;
                        case Action.CD:
                            if (string.IsNullOrEmpty(path))
                            {
                                throw new ScriptException(ScriptError.NO_PATH_PROVIDED);
                            }
                            switch (!path[0].Equals(CORRADE_CONSTANTS.PATH_SEPARATOR[0]))
                            {
                                case true:
                                    lock (GroupDirectoryTrackersLock)
                                    {
                                        item =
                                            GroupDirectoryTrackers[corradeCommandParameters.Group.UUID] as InventoryBase;
                                    }
                                    break;
                                default:
                                    item = Client.Inventory.Store.RootFolder;
                                    break;
                            }
                            item = findPath(path, item);
                            if (!(item is InventoryFolder))
                            {
                                throw new ScriptException(ScriptError.UNEXPECTED_ITEM_IN_PATH);
                            }
                            lock (GroupDirectoryTrackersLock)
                            {
                                GroupDirectoryTrackers[corradeCommandParameters.Group.UUID] = item;
                            }
                            break;
                        case Action.MKDIR:
                            string mkdirName =
                                wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.NAME)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(mkdirName))
                            {
                                throw new ScriptException(ScriptError.NO_NAME_PROVIDED);
                            }
                            switch (!string.IsNullOrEmpty(path))
                            {
                                case true:
                                    if (path[0].Equals(CORRADE_CONSTANTS.PATH_SEPARATOR[0]))
                                    {
                                        item = Client.Inventory.Store.RootFolder;
                                        break;
                                    }
                                    goto default;
                                default:
                                    lock (GroupDirectoryTrackersLock)
                                    {
                                        item =
                                            GroupDirectoryTrackers[corradeCommandParameters.Group.UUID] as InventoryBase;
                                    }
                                    break;
                            }
                            item = findPath(path, item);
                            if (!(item is InventoryFolder))
                            {
                                throw new ScriptException(ScriptError.UNEXPECTED_ITEM_IN_PATH);
                            }
                            if (Client.Inventory.CreateFolder(item.UUID, mkdirName) == UUID.Zero)
                            {
                                throw new ScriptException(ScriptError.UNABLE_TO_CREATE_FOLDER);
                            }
                            UpdateInventoryRecursive.Invoke(Client.Inventory.Store.RootFolder);
                            break;
                        case Action.CHMOD:
                            string itemPermissions =
                                wasInput(
                                    wasKeyValueGet(
                                        wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.PERMISSIONS)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(itemPermissions))
                            {
                                throw new ScriptException(ScriptError.NO_PERMISSIONS_PROVIDED);
                            }
                            switch (!string.IsNullOrEmpty(path))
                            {
                                case true:
                                    if (path[0].Equals(CORRADE_CONSTANTS.PATH_SEPARATOR[0]))
                                    {
                                        item = Client.Inventory.Store.RootFolder;
                                        break;
                                    }
                                    goto default;
                                default:
                                    lock (GroupDirectoryTrackersLock)
                                    {
                                        item =
                                            GroupDirectoryTrackers[corradeCommandParameters.Group.UUID] as InventoryBase;
                                    }
                                    break;
                            }
                            item = findPath(path, item);
                            switch (item is InventoryFolder)
                            {
                                case true:
                                    if (Client.Inventory.Store.GetContents(
                                        item.UUID)
                                        .OfType<InventoryItem>()
                                        .Any(
                                            inventoryItem =>
                                                !wasSetInventoryItemPermissions(inventoryItem, itemPermissions)))
                                    {
                                        throw new ScriptException(ScriptError.SETTING_PERMISSIONS_FAILED);
                                    }
                                    break;
                                default:
                                    if (!wasSetInventoryItemPermissions((item as InventoryItem), itemPermissions))
                                    {
                                        throw new ScriptException(ScriptError.SETTING_PERMISSIONS_FAILED);
                                    }
                                    break;
                            }
                            UpdateInventoryRecursive.Invoke(Client.Inventory.Store.RootFolder);
                            break;
                        case Action.RM:
                            switch (!string.IsNullOrEmpty(path))
                            {
                                case true:
                                    if (path[0].Equals(CORRADE_CONSTANTS.PATH_SEPARATOR[0]))
                                    {
                                        item = Client.Inventory.Store.RootFolder;
                                        break;
                                    }
                                    goto default;
                                default:
                                    lock (GroupDirectoryTrackersLock)
                                    {
                                        item =
                                            GroupDirectoryTrackers[corradeCommandParameters.Group.UUID] as InventoryBase;
                                    }
                                    break;
                            }
                            item = findPath(path, item);
                            switch (item is InventoryFolder)
                            {
                                case true:
                                    Client.Inventory.MoveFolder(item.UUID,
                                        Client.Inventory.FindFolderForType(AssetType.TrashFolder));
                                    break;
                                default:
                                    Client.Inventory.MoveItem(item.UUID,
                                        Client.Inventory.FindFolderForType(AssetType.TrashFolder));
                                    break;
                            }
                            UpdateInventoryRecursive.Invoke(Client.Inventory.Store.RootFolder);
                            break;
                        case Action.CP:
                        case Action.MV:
                        case Action.LN:
                            string lnSourcePath =
                                wasInput(wasKeyValueGet(
                                    wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.SOURCE)),
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
                                            GroupDirectoryTrackers[corradeCommandParameters.Group.UUID] as InventoryBase;
                                    }
                                    break;
                            }
                            sourceItem = findPath(lnSourcePath, sourceItem);
                            switch (action)
                            {
                                case Action.CP:
                                case Action.LN:
                                    if (sourceItem is InventoryFolder)
                                    {
                                        throw new ScriptException(ScriptError.EXPECTED_ITEM_AS_SOURCE);
                                    }
                                    break;
                            }
                            string lnTargetPath =
                                wasInput(wasKeyValueGet(
                                    wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TARGET)),
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
                                            GroupDirectoryTrackers[corradeCommandParameters.Group.UUID] as InventoryBase;
                                    }
                                    break;
                            }
                            targetItem = findPath(lnTargetPath, targetItem);
                            if (!(targetItem is InventoryFolder))
                            {
                                throw new ScriptException(ScriptError.EXPECTED_FOLDER_AS_TARGET);
                            }
                            switch (action)
                            {
                                case Action.LN:
                                    Client.Inventory.CreateLink(targetItem.UUID, sourceItem, (succeeded, newItem) =>
                                    {
                                        if (!succeeded)
                                        {
                                            throw new ScriptException(ScriptError.UNABLE_TO_CREATE_ITEM);
                                        }
                                        Client.Inventory.RequestFetchInventory(newItem.UUID, newItem.OwnerID);
                                    });
                                    break;
                                case Action.MV:
                                    switch (sourceItem is InventoryFolder)
                                    {
                                        case true:
                                            Client.Inventory.MoveFolder(sourceItem.UUID, targetItem.UUID);
                                            break;
                                        default:
                                            Client.Inventory.MoveItem(sourceItem.UUID, targetItem.UUID);
                                            break;
                                    }
                                    break;
                                case Action.CP:
                                    Client.Inventory.RequestCopyItem(sourceItem.UUID, targetItem.UUID,
                                        sourceItem.Name,
                                        newItem =>
                                        {
                                            if (newItem == null)
                                            {
                                                throw new ScriptException(ScriptError.UNABLE_TO_CREATE_ITEM);
                                            }
                                        });
                                    break;
                            }
                            UpdateInventoryRecursive.Invoke(Client.Inventory.Store.RootFolder);
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                    if (csv.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(csv));
                    }
                };
        }
    }
}