///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;
using OpenMetaverse;
using wasSharp;

namespace wasOpenMetaverse
{
    public static class Inventory
    {
        /// <summary>
        ///     Can an inventory item be worn?
        /// </summary>
        /// <param name="item">item to check</param>
        /// <returns>true if the inventory item can be worn</returns>
        public static bool CanBeWorn(InventoryBase item)
        {
            return item is InventoryWearable || item is InventoryAttachment || item is InventoryObject;
        }

        /// <summary>
        ///     Resolves inventory links and returns a real inventory item that
        ///     the link is pointing to
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="item">a link or inventory item</param>
        /// <returns>the real inventory item</returns>
        public static InventoryItem ResolveItemLink(GridClient Client, InventoryItem item)
        {
            Locks.ClientInstanceInventoryLock.EnterReadLock();
            var inventoryItem = item.IsLink() && Client.Inventory.Store.Contains(item.AssetUUID) &&
                   Client.Inventory.Store[item.AssetUUID] is InventoryItem
                ? (InventoryItem)Client.Inventory.Store[item.AssetUUID]
                : item;
            Locks.ClientInstanceInventoryLock.ExitReadLock();
            return inventoryItem;
        }

        /// <summary>
        ///     Get current outfit folder links.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="outfitFolder">the outfit folder to return items from</param>
        /// <param name="millisecondsTimeout">the timeout in milliseconds</param>
        /// <returns>a list of inventory items that can be part of appearance (attachments, wearables)</returns>
        public static IEnumerable<InventoryItem> GetCurrentOutfitFolderLinks(GridClient Client,
            InventoryFolder outfitFolder, uint millisecondsTimeout)
        {
            UpdateInventoryRecursive(Client, outfitFolder, millisecondsTimeout, true);
            Locks.ClientInstanceInventoryLock.EnterReadLock();
            var inventoryItems = Client.Inventory.Store.GetContents(outfitFolder)
                    .AsParallel()
                    .Where(o => CanBeWorn(o) && ((InventoryItem)o).AssetType.Equals(AssetType.Link))
                    .Select(o => o as InventoryItem);
            Locks.ClientInstanceInventoryLock.ExitReadLock();
            return inventoryItems;
        }

        public static void Attach(GridClient Client, InventoryFolder CurrentOutfitFolder, InventoryItem item,
            AttachmentPoint point, bool replace, uint millisecondsTimeout)
        {
            var realItem = ResolveItemLink(Client, item);
            if (!(realItem is InventoryAttachment) && !(realItem is InventoryObject)) return;
            var attachmentPoint = AttachmentPoint.Default;
            Locks.ClientInstanceAppearanceLock.EnterWriteLock();
            var objectAttachedEvent = new ManualResetEvent(false);
            EventHandler<PrimEventArgs> ObjectUpdateEventHandler = (sender, args) =>
            {
                Primitive prim;
                Locks.ClientInstanceNetworkLock.EnterReadLock();
                if (!Client.Network.CurrentSim.ObjectsPrimitives.TryGetValue(args.Prim.LocalID, out prim))
                {
                    Locks.ClientInstanceNetworkLock.ExitReadLock();
                    return;
                }
                Locks.ClientInstanceNetworkLock.ExitReadLock();

                if (Client.Self.LocalID.Equals(0) || prim.NameValues == null)
                    return;

                if (!prim.NameValues.AsParallel()
                    .Where(o => string.Equals(o.Name, "AttachItemID"))
                    .Any(o => string.Equals(o.Value.ToString().Trim(), realItem.UUID.ToString(),
                        StringComparison.OrdinalIgnoreCase))) return;

                attachmentPoint = (AttachmentPoint)(((prim.PrimData.State & 0xF0) >> 4) |
                                                     ((prim.PrimData.State & ~0xF0) << 4));

                objectAttachedEvent.Set();
            };

            Locks.ClientInstanceObjectsLock.EnterWriteLock();
            Client.Objects.ObjectUpdate += ObjectUpdateEventHandler;
            Client.Appearance.Attach(realItem, point, replace);
            objectAttachedEvent.WaitOne((int)millisecondsTimeout, true);
            Client.Objects.ObjectUpdate -= ObjectUpdateEventHandler;
            Locks.ClientInstanceObjectsLock.ExitWriteLock();
            Locks.ClientInstanceAppearanceLock.ExitWriteLock();
            if (realItem is InventoryAttachment)
            {
                (realItem as InventoryAttachment).AttachmentPoint = attachmentPoint;
            }
            if (realItem is InventoryObject)
            {
                (realItem as InventoryObject).AttachPoint = attachmentPoint;
            }
            Client.Inventory.RequestUpdateItem(realItem);
            AddLink(Client, realItem, CurrentOutfitFolder, millisecondsTimeout);
            UpdateInventoryRecursive(Client, CurrentOutfitFolder, millisecondsTimeout, true);
        }

        public static void Detach(GridClient Client, InventoryFolder CurrentOutfitFolder, InventoryItem item,
            uint millisecondsTimeout)
        {
            var realItem = ResolveItemLink(Client, item);
            if (!(realItem is InventoryAttachment) && !(realItem is InventoryObject)) return;
            RemoveLink(Client, realItem, CurrentOutfitFolder, millisecondsTimeout);
            var attachmentPoint = AttachmentPoint.Default;
            Locks.ClientInstanceAppearanceLock.EnterWriteLock();
            var objectDetachedEvent = new ManualResetEvent(false);
            EventHandler<KillObjectEventArgs> KillObjectEventHandler = (sender, args) =>
            {
                Primitive prim;
                Locks.ClientInstanceNetworkLock.EnterReadLock();
                if (!Client.Network.CurrentSim.ObjectsPrimitives.TryGetValue(args.ObjectLocalID, out prim))
                {
                    Locks.ClientInstanceNetworkLock.ExitReadLock();
                    return;
                }
                Locks.ClientInstanceNetworkLock.ExitReadLock();

                if (Client.Self.LocalID.Equals(0) || prim.NameValues == null)
                    return;

                if (!prim.NameValues.AsParallel()
                    .Where(o => string.Equals(o.Name, "AttachItemID"))
                    .Any(o => string.Equals(o.Value.ToString().Trim(), realItem.UUID.ToString(),
                        StringComparison.OrdinalIgnoreCase))) return;

                attachmentPoint = (AttachmentPoint)(((prim.PrimData.State & 0xF0) >> 4) |
                                                     ((prim.PrimData.State & ~0xF0) << 4));

                objectDetachedEvent.Set();
            };

            Locks.ClientInstanceObjectsLock.EnterWriteLock();
            Client.Objects.KillObject += KillObjectEventHandler;
            Client.Appearance.Detach(realItem);
            objectDetachedEvent.WaitOne((int)millisecondsTimeout, true);
            Client.Objects.KillObject -= KillObjectEventHandler;
            Locks.ClientInstanceObjectsLock.ExitWriteLock();
            Locks.ClientInstanceAppearanceLock.ExitWriteLock();
            if (realItem is InventoryAttachment)
            {
                (realItem as InventoryAttachment).AttachmentPoint = attachmentPoint;
            }
            if (realItem is InventoryObject)
            {
                (realItem as InventoryObject).AttachPoint = attachmentPoint;
            }
            Client.Inventory.RequestUpdateItem(realItem);
            UpdateInventoryRecursive(Client, CurrentOutfitFolder, millisecondsTimeout, true);
        }

        public static void Wear(GridClient Client, InventoryFolder CurrentOutfitFolder, InventoryItem item, bool replace,
            uint millisecondsTimeout)
        {
            var realItem = ResolveItemLink(Client, item);
            if (!(realItem is InventoryWearable)) return;
            Locks.ClientInstanceAppearanceLock.EnterWriteLock();
            Client.Appearance.AddToOutfit(realItem, replace);
            Locks.ClientInstanceAppearanceLock.ExitWriteLock();
            AddLink(Client, realItem, CurrentOutfitFolder, millisecondsTimeout);
            UpdateInventoryRecursive(Client, CurrentOutfitFolder, millisecondsTimeout, true);
        }

        public static void UnWear(GridClient Client, InventoryFolder CurrentOutfitFolder, InventoryItem item,
            uint millisecondsTimeout)
        {
            var realItem = ResolveItemLink(Client, item);
            if (!(realItem is InventoryWearable)) return;
            Locks.ClientInstanceAppearanceLock.EnterWriteLock();
            Client.Appearance.RemoveFromOutfit(realItem);
            Locks.ClientInstanceAppearanceLock.ExitWriteLock();
            RemoveLink(Client, realItem, CurrentOutfitFolder, millisecondsTimeout);
            UpdateInventoryRecursive(Client, CurrentOutfitFolder, millisecondsTimeout, true);
        }

        /// <summary>
        ///     Is the item a body part?
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="item">the item to check</param>
        /// <returns>true if the item is a body part</returns>
        public static bool IsBodyPart(GridClient Client, InventoryItem item)
        {
            var realItem = ResolveItemLink(Client, item);
            if (!(realItem is InventoryWearable)) return false;
            var t = ((InventoryWearable)realItem).WearableType;
            return t.Equals(WearableType.Shape) ||
                   t.Equals(WearableType.Skin) ||
                   t.Equals(WearableType.Eyes) ||
                   t.Equals(WearableType.Hair);
        }

        /// <summary>
        ///     Creates a new current outfit folder link.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="item">item to be linked</param>
        /// <param name="outfitFolder">the outfit folder</param>
        /// <param name="millisecondsTimeout">the timeout in milliseconds</param>
        public static void AddLink(GridClient Client, InventoryItem item, InventoryFolder outfitFolder,
            uint millisecondsTimeout)
        {
            if (outfitFolder == null) return;

            if (
                !GetCurrentOutfitFolderLinks(Client, outfitFolder, millisecondsTimeout)
                    .AsParallel()
                    .Any(o => o.AssetUUID.Equals(item.UUID)))
            {
                Locks.ClientInstanceInventoryLock.EnterWriteLock();
                Client.Inventory.CreateLink(outfitFolder.UUID, item.UUID, item.Name,
                        item.InventoryType.Equals(InventoryType.Wearable) && !IsBodyPart(Client, item)
                            ? $"@{(int)((InventoryWearable)item).WearableType}{0:00}"
                            : string.Empty, AssetType.Link, item.InventoryType, UUID.Random(), (success, newItem) =>
                            {
                                if (success)
                                {
                                    Client.Inventory.RequestFetchInventory(newItem.UUID, newItem.OwnerID);
                                }
                            });
                Client.Inventory.Store.GetNodeFor(outfitFolder.UUID).NeedsUpdate = true;
                Locks.ClientInstanceInventoryLock.ExitWriteLock();
            }
        }

        /// <summary>
        ///     Remove current outfit folder links for multiple specified inventory item.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="item">the item whose link should be removed</param>
        /// <param name="outfitFolder">the outfit folder</param>
        /// <param name="millisecondsTimeout">the timeout in milliseconds</param>
        private static void RemoveLink(GridClient Client, InventoryItem item, InventoryFolder outfitFolder,
            uint millisecondsTimeout)
        {
            if (outfitFolder == null) return;

            var contents =
                new List<InventoryItem>(GetCurrentOutfitFolderLinks(Client, outfitFolder, millisecondsTimeout));
            Locks.ClientInstanceInventoryLock.EnterWriteLock();
            Client.Inventory.Remove(
                    contents
                        .AsParallel()
                        .Where(o => o.AssetUUID.Equals(item.UUID))
                        .Select(o => o.UUID)
                        .ToList(), null);
            Client.Inventory.Store.GetNodeFor(outfitFolder.UUID).NeedsUpdate = true;
            Locks.ClientInstanceInventoryLock.ExitWriteLock();
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get all worn attachments.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="dataTimeout">the alarm timeout for receiving object properties</param>
        /// <returns>attachment points by primitives</returns>
        public static IEnumerable<KeyValuePair<Primitive, AttachmentPoint>> GetAttachments(GridClient Client,
            uint dataTimeout)
        {
            Locks.ClientInstanceNetworkLock.EnterReadLock();
            var selectedPrimitives = new HashSet<Primitive>(Client.Network.Simulators.AsParallel()
                    .Select(o => o.ObjectsPrimitives)
                    .Select(o => o.Copy().Values)
                    .SelectMany(o => o)
                    .Where(o => o.ParentID.Equals(Client.Self.LocalID)));
            Locks.ClientInstanceNetworkLock.ExitReadLock();

            if (!selectedPrimitives.Any() || !Services.UpdatePrimitives(Client, ref selectedPrimitives, dataTimeout))
                return Enumerable.Empty<KeyValuePair<Primitive, AttachmentPoint>>();
            return selectedPrimitives
                .AsParallel()
                .Select(o => new KeyValuePair<Primitive, AttachmentPoint>(o,
                    (AttachmentPoint)(((o.PrimData.State & 0xF0) >> 4) |
                                       ((o.PrimData.State & ~0xF0) << 4))));
        }

        /// <summary>
        ///     Get the inventory item from an attached primitive.
        /// </summary>
        /// <param name="Client">the GridClient to use</param>
        /// <param name="prim">Prim to check</param>
        /// <returns>the inventory item if found or null otherwise</returns>
        public static InventoryItem GetAttachedInventoryItem(GridClient Client, Primitive prim)
        {
            if (prim.NameValues == null) return null;

            for (var i = 0; i < prim.NameValues.Length; i++)
            {
                if (prim.NameValues[i].Name.Equals("AttachItemID")) continue;
                Locks.ClientInstanceInventoryLock.EnterReadLock();
                var inventoryItem = Client.Inventory.Store[new UUID(prim.NameValues[i].Value.ToString())] as InventoryItem;
                Locks.ClientInstanceInventoryLock.ExitReadLock();
                return inventoryItem;
            }
            return null;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets the inventory wearables that are currently being worn.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="outfitFolder">the folder to start the search from</param>
        /// <param name="millisecondsTimeout">the timeout in milliseconds</param>
        /// <returns>the worn inventory itemse</returns>
        public static IEnumerable<InventoryItem> GetWearables(GridClient Client, InventoryFolder outfitFolder,
            uint millisecondsTimeout)
        {
            return outfitFolder != null
                ? GetCurrentOutfitFolderLinks(Client, outfitFolder, millisecondsTimeout)
                    .AsParallel()
                    .Select(o => ResolveItemLink(Client, o))
                    .Where(o => o is InventoryWearable)
                    .Select(o => o)
                : Enumerable.Empty<InventoryItem>();
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Find an inventory item or inventory folder by path.
        /// </summary>
        /// <param name="root">the directory from which to start searching</param>
        /// <param name="Client">the grid client to use</param>
        /// <param name="path">the full path to the item</param>
        /// <param name="separator">the path separator character</param>
        /// <param name="escape">the path escape character</param>
        /// <param name="millisecondsTimeout">the time in milliseconds for requesting folder items</param>
        /// <param name="comparison">which string comparison to use for named path parts</param>
        /// <returns>an inventory base item if found or null otherwise</returns>
        /// <remarks>in case the path is ambiguous, the function returns null</remarks>
        private static InventoryBase directFindInventory(GridClient Client, string path, char separator, char? escape,
            uint millisecondsTimeout, InventoryBase root = null, StringComparison comparison = StringComparison.Ordinal)
        {
            if (string.IsNullOrEmpty(path)) return root;

            // Split all paths.
            var unpack = new List<string>(path.PathSplit(separator, escape, false));
            // Pop first item to process.
            var first = unpack.First();

            // Avoid preceeding slashes.
            if (string.IsNullOrEmpty(first)) goto CONTINUE;

            // If root is null assume that the starting point is above both inventory and library folder.
            if (root == null)
            {
                UUID firstUUID;
                switch (UUID.TryParse(first, out firstUUID))
                {
                    case true: // There is no root and first of the path is an UUID, hmm...
                        // If the first part of the path is the inventory folder...
                        if (Client.Inventory.Store.RootFolder.UUID.Equals(firstUUID))
                        {
                            // ..set the root to the inventory folder.
                            root = Client.Inventory.Store.RootFolder;
                            break;
                        }
                        // If the first part of the path is the library folder...
                        if (Client.Inventory.Store.LibraryFolder.UUID.Equals(firstUUID))
                        {
                            // .. set the root to the library folder.
                            root = Client.Inventory.Store.LibraryFolder;
                            break;
                        }
                        // If not, the path is phony!
                        return null;

                    default: // There is no root and the first of the path is a name, hmm...
                        if (string.Equals(Client.Inventory.Store.RootFolder.Name, first, comparison))
                        {
                            // ..set the root to the inventory folder.
                            root = Client.Inventory.Store.RootFolder;
                            break;
                        }
                        if (string.Equals(Client.Inventory.Store.LibraryFolder.Name, first, comparison))
                        {
                            // .. set the root to the library folder.
                            root = Client.Inventory.Store.LibraryFolder;
                            break;
                        }
                        // If not, the path is phony!
                        return null;
                }

                goto CONTINUE;
            }

            var rootNode = Client.Inventory.Store.GetNodeFor(root.UUID);
            if (rootNode == null)
                return null;
            var contents = new HashSet<InventoryBase>();
            switch (rootNode.NeedsUpdate)
            {
                case true:
                    contents.UnionWith(Client.Inventory.FolderContents(root.UUID, root.OwnerID, true, true,
                        InventorySortOrder.ByDate, (int)millisecondsTimeout));
                    break;

                default:
                    contents.UnionWith(Client.Inventory.Store.GetContents(root.UUID));
                    break;
            }

            UUID itemUUID;
            switch (!UUID.TryParse(first, out itemUUID))
            {
                case true:
                    try
                    {
                        root = contents.SingleOrDefault(q => string.Equals(q.Name, first, comparison));
                        break;
                    }
                    catch (Exception)
                    {
                        // ambiguous path
                        return null;
                    }
                default:
                    root = contents.FirstOrDefault(q => q.UUID.Equals(itemUUID));
                    break;
            }

            if (root is InventoryItem)
            {
                return root;
            }

            if (root == null)
            {
                return null;
            }

        CONTINUE:
            return directFindInventory(Client,
                string.Join(separator.ToString(),
                    unpack.Skip(1)
                        .Select(o => string.Join(escape?.ToString() + separator.ToString(), o.Split(separator)))),
                separator, escape,
                millisecondsTimeout,
                root, comparison);
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Find an inventory item or inventory folder by path.
        /// </summary>
        /// <typeparam name="T">the type of inventory to find</typeparam>
        /// <param name="root">the directory from which to start searching</param>
        /// <param name="Client">the grid client to use</param>
        /// <param name="path">the full path to the item</param>
        /// <param name="separator">the path separator character</param>
        /// <param name="escape">the path escape character</param>
        /// <param name="millisecondsTimeout">the time in milliseconds for requesting folder items</param>
        /// <param name="comparison">what comparison to use on string type path parts</param>
        /// <returns>an inventory item of type T</returns>
        /// <remarks>in case the path is ambiguous, the function returns null</remarks>
        public static T FindInventory<T>(GridClient Client, string path, char separator, char? escape,
            uint millisecondsTimeout, InventoryFolder root = null,
            StringComparison comparison = StringComparison.Ordinal)
        {
            InventoryBase inventoryBase;
            Locks.ClientInstanceInventoryLock.EnterReadLock();
            inventoryBase = directFindInventory(Client, path, separator, escape, millisecondsTimeout, root,
                    comparison);
            Locks.ClientInstanceInventoryLock.ExitReadLock();

            if (inventoryBase == null)
                return default(T);

            if (typeof(T) != typeof(InventoryNode)) return (T)(object)inventoryBase;

            Locks.ClientInstanceInventoryLock.EnterReadLock();
            var inventoryNode = (T)(object)Client.Inventory.Store.GetNodeFor(inventoryBase.UUID);
            Locks.ClientInstanceInventoryLock.ExitReadLock();
            return inventoryNode;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// ///
        /// <summary>
        ///     Fetches items by searching the inventory starting with an inventory
        ///     node where the search criteria finds:
        ///     - name as string
        ///     - name as Regex
        ///     - UUID as UUID
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="root">the node to start the search from</param>
        /// <param name="criteria">the name, UUID or Regex of the item to be found</param>
        /// <param name="millisecondsTimeout">the timeout for each folder update in milliseconds</param>
        /// <returns>a list of items matching the item name</returns>
        private static IEnumerable<T> directFindInventory<T>(GridClient Client, InventoryNode root, Regex criteria,
            uint millisecondsTimeout)
        {
            var rootFolder = Client.Inventory.Store[root.Data.UUID] as InventoryFolder;
            if (rootFolder == null)
                yield break;
            var inventoryFolders = new HashSet<InventoryFolder>();
            foreach (var item in Client.Inventory.Store.GetContents(rootFolder))
            {
                if (criteria.IsMatch(item.Name))
                {
                    if (typeof(T) == typeof(InventoryNode))
                    {
                        yield return (T)(object)Client.Inventory.Store.GetNodeFor(item.UUID);
                    }
                    if (typeof(T) == typeof(InventoryBase))
                    {
                        yield return (T)(object)item;
                    }
                }
                if (item is InventoryFolder)
                {
                    inventoryFolders.Add(item as InventoryFolder);
                }
            }
            foreach (var folder in inventoryFolders)
            {
                var folderNode = Client.Inventory.Store.GetNodeFor(folder.UUID);
                if (folderNode == null)
                    continue;
                if (folderNode.NeedsUpdate)
                {
                    var FolderUpdatedEvent = new ManualResetEvent(false);
                    EventHandler<FolderUpdatedEventArgs> FolderUpdatedEventHandler = (p, q) => FolderUpdatedEvent.Set();
                    Client.Inventory.FolderUpdated += FolderUpdatedEventHandler;
                    FolderUpdatedEvent.Reset();
                    Client.Inventory.RequestFolderContents(folder.UUID, Client.Self.AgentID, true, true,
                        InventorySortOrder.ByDate);
                    FolderUpdatedEvent.WaitOne((int)millisecondsTimeout, true);
                    Client.Inventory.FolderUpdated -= FolderUpdatedEventHandler;
                }
                foreach (var o in directFindInventory<T>(Client, folderNode, criteria, millisecondsTimeout))
                {
                    yield return o;
                }
            }
        }

        public static IEnumerable<T> FindInventory<T>(GridClient Client, InventoryNode root, Regex criteria,
            uint millisecondsTimeout)
        {
            Locks.ClientInstanceInventoryLock.EnterReadLock();
            var inventoryitems = directFindInventory<T>(Client, root, criteria, millisecondsTimeout);
            Locks.ClientInstanceInventoryLock.ExitReadLock();
            return inventoryitems;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Fetches items and their full path from the inventory starting with
        ///     an inventory node where the search criteria is an UUID.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="root">the node to start the search from</param>
        /// <param name="criteria">UUID of the item to be found</param>
        /// <param name="prefix">any prefix to append to the found paths</param>
        /// <returns>items matching criteria and their full inventoy path</returns>
        private static IEnumerable<KeyValuePair<T, LinkedList<string>>> directFindInventoryPath<T>(GridClient Client,
            InventoryNode root, UUID criteria, LinkedList<string> prefix)
        {
            if (criteria.Equals(root.Data.UUID) ||
                (Client.Inventory.Store[root.Data.UUID] is InventoryItem &&
                 (Client.Inventory.Store[root.Data.UUID] as InventoryItem).AssetUUID.Equals(criteria)))
            {
                if (typeof(T) == typeof(InventoryBase))
                {
                    yield return
                        new KeyValuePair<T, LinkedList<string>>((T)(object)Client.Inventory.Store[root.Data.UUID],
                            new LinkedList<string>(
                                prefix.Concat(new[] { root.Data.Name })));
                }
                if (typeof(T) == typeof(InventoryNode))
                {
                    yield return
                        new KeyValuePair<T, LinkedList<string>>((T)(object)root,
                            new LinkedList<string>(
                                prefix.Concat(new[] { root.Data.Name })));
                }
            }
            foreach (
                var o in
                    root.Nodes.Values.AsParallel()
                        .SelectMany(o => directFindInventoryPath<T>(Client, o, criteria, new LinkedList<string>(
                            prefix.Concat(new[] { root.Data.Name })))))
            {
                yield return o;
            }
        }

        public static IEnumerable<KeyValuePair<T, LinkedList<string>>> FindInventoryPath<T>(GridClient Client,
            InventoryNode root, UUID criteria, LinkedList<string> prefix)
        {
            Locks.ClientInstanceInventoryLock.EnterReadLock();
            var inventoryPaths = directFindInventoryPath<T>(Client, root, criteria, prefix);
            Locks.ClientInstanceInventoryLock.ExitReadLock();
            return inventoryPaths;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Fetches items and their full path from the inventory starting with
        ///     an inventory node where the search criteria is a regex.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="root">the node to start the search from</param>
        /// <param name="criteria">Regex for the item to be found</param>
        /// <param name="prefix">any prefix to append to the found paths</param>
        /// <returns>items matching criteria and their full inventoy path</returns>
        private static IEnumerable<KeyValuePair<T, LinkedList<string>>> directFindInventoryPath<T>(GridClient Client,
            InventoryNode root, Regex criteria, LinkedList<string> prefix)
        {
            if (criteria.IsMatch(root.Data.Name))
            {
                if (typeof(T) == typeof(InventoryBase))
                {
                    yield return
                        new KeyValuePair<T, LinkedList<string>>((T)(object)Client.Inventory.Store[root.Data.UUID],
                            new LinkedList<string>(
                                prefix.Concat(new[] { root.Data.Name })));
                }
                if (typeof(T) == typeof(InventoryNode))
                {
                    yield return
                        new KeyValuePair<T, LinkedList<string>>((T)(object)root,
                            new LinkedList<string>(
                                prefix.Concat(new[] { root.Data.Name })));
                }
            }
            foreach (
                var o in
                    root.Nodes.Values.AsParallel()
                        .SelectMany(o => directFindInventoryPath<T>(Client, o, criteria, new LinkedList<string>(
                            prefix.Concat(new[] { root.Data.Name })))))
            {
                yield return o;
            }
        }

        public static IEnumerable<KeyValuePair<T, LinkedList<string>>> FindInventoryPath<T>(GridClient Client,
            InventoryNode root, Regex criteria, LinkedList<string> prefix)
        {
            Locks.ClientInstanceInventoryLock.EnterReadLock();
            var inventoryPaths = directFindInventoryPath<T>(Client, root, criteria, prefix);
            Locks.ClientInstanceInventoryLock.ExitReadLock();
            return inventoryPaths;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Get the path to an inventory item starting with a root folder.
        /// </summary>
        /// <param name="item">the item to which to get the path</param>
        /// <param name="Client">the grid client to use</param>
        /// <param name="root">the root folder</param>
        /// <param name="separator">the separator to use for the path</param>
        /// <returns>the path to the item or the empty string</returns>
        public static string GetInventoryPath(this InventoryBase item, GridClient Client, InventoryFolder root,
            char separator)
        {
            if (item == null) return string.Empty;

            var path = new List<string>();

            do
            {
                path.Insert(0, item.Name);

                if (item.ParentUUID.Equals(UUID.Zero))
                    break;

                item = Client.Inventory.Store[item.ParentUUID];
            } while (item != null);

            return path.Contains(root.Name)
                ? string.Join(separator.ToString(), path.Skip(path.IndexOf(root.Name) + 1))
                : string.Empty;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2016 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns a list of inventory items or inventory folders by searching recursively starting from a folder.
        /// </summary>
        /// <param name="root">the root folder from where to start the search</param>
        /// <param name="Client">the grid client to use for the search</param>
        /// <param name="millisecondsTimeout">the timeout in milliseconds for requesting folder contents</param>
        /// <returns>a list of inventory items and folders</returns>
        public static IEnumerable<InventoryBase> GetInventoryRecursive(this InventoryFolder root, GridClient Client,
            uint millisecondsTimeout)
        {
            // Create the queue of folders.
            var inventoryFolders = new BlockingQueue<InventoryFolder>();
            // Enqueue the first folder (root).
            inventoryFolders.Enqueue(root);

            UUID clientUUID;
            clientUUID = Client.Self.AgentID;

            do
            {
                // Dequeue folder.
                var queueFolder = inventoryFolders.Dequeue();
                var contents = new HashSet<InventoryBase>();
                Locks.ClientInstanceInventoryLock.EnterReadLock();
                contents.UnionWith(Client.Inventory.FolderContents(queueFolder.UUID, clientUUID, true, true,
                        InventorySortOrder.ByDate, (int)millisecondsTimeout));
                Locks.ClientInstanceInventoryLock.ExitReadLock();
                foreach (var item in contents)
                {
                    if (item is InventoryFolder)
                        inventoryFolders.Enqueue(item as InventoryFolder);

                    yield return item;
                }
            } while (inventoryFolders.Any());
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Update inventory nodes recursively starting from a node.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="root">the root folder from where to start updating</param>
        /// <param name="millisecondsTimeout">the timeout for updating folders in milliseconds</param>
        /// <param name="force">true if the node status should be ignored</param>
        private static void directUpdateInventoryRecursive(GridClient Client, InventoryNode root,
            uint millisecondsTimeout, bool force = false)
        {
            var inventoryFolders = new List<InventoryNode>();
            if (root.Nodes.Values.Any(node => node.Data is InventoryFolder && node.NeedsUpdate) || root.NeedsUpdate ||
                !force)
            {
                var FolderUpdatedEvent = new AutoResetEvent(false);
                var LockObject = new object();
                EventHandler<FolderUpdatedEventArgs> FolderUpdatedEventHandler = (p, q) =>
                {
                    if (!q.FolderID.Equals(root.Data.UUID)) return;

                    lock (LockObject)
                    {
                        inventoryFolders.AddRange(
                            Client.Inventory.Store.GetContents(q.FolderID)
                                .AsParallel()
                                .Where(o => o is InventoryFolder)
                                .Select(o => Client.Inventory.Store.GetNodeFor(o.UUID)));
                    }
                    FolderUpdatedEvent.Set();
                };

                Client.Inventory.FolderUpdated += FolderUpdatedEventHandler;
                Client.Inventory.RequestFolderContents(root.Data.UUID, root.Data.OwnerID, true, true,
                    InventorySortOrder.ByDate);
                FolderUpdatedEvent.WaitOne((int)millisecondsTimeout, true);
                Client.Inventory.FolderUpdated -= FolderUpdatedEventHandler;
            }

            // Radegast does this with 6 threads, any reason?
            inventoryFolders.AsParallel()
                .ForAll(o => { directUpdateInventoryRecursive(Client, o, millisecondsTimeout, force); });
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Update all inventory items starting from a folder.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="root">the root folder from where to start updating</param>
        /// <param name="millisecondsTimeout">the timeout for updating folders in milliseconds</param>
        /// <param name="force">true if the node status should be ignored</param>
        public static void UpdateInventoryRecursive(GridClient Client, InventoryFolder root,
            uint millisecondsTimeout, bool force = false)
        {
            Locks.ClientInstanceNetworkLock.EnterReadLock();
            // Check if we are connected.
            if (!Client.Network.Connected)
            {
                Locks.ClientInstanceNetworkLock.ExitReadLock();
                return;
            }
            // Wait for CAPs.
            if (!Client.Network.CurrentSim.Caps.IsEventQueueRunning)
            {
                var EventQueueRunningEvent = new AutoResetEvent(false);
                EventHandler<EventQueueRunningEventArgs> handler = (sender, e) => { EventQueueRunningEvent.Set(); };
                Client.Network.EventQueueRunning += handler;
                EventQueueRunningEvent.WaitOne((int)millisecondsTimeout, true);
                Client.Network.EventQueueRunning -= handler;
            }
            Locks.ClientInstanceNetworkLock.ExitReadLock();

            Locks.ClientInstanceInventoryLock.EnterWriteLock();
            directUpdateInventoryRecursive(Client, Client.Inventory.Store.GetNodeFor(root.UUID), millisecondsTimeout,
                    force);
            Locks.ClientInstanceInventoryLock.ExitWriteLock();
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Update all inventory items starting from a folder.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="root">the root folder from where to start updating</param>
        /// <param name="millisecondsTimeout">the timeout for updating folders in milliseconds</param>
        /// <param name="force">true if the node status should be ignored</param>
        public static void UpdateInventoryRecursive(GridClient Client, InventoryNode root,
            uint millisecondsTimeout, bool force = false)
        {
            Locks.ClientInstanceNetworkLock.EnterReadLock();
            // Check if we are connected.
            if (!Client.Network.Connected)
            {
                Locks.ClientInstanceNetworkLock.ExitReadLock();
                return;
            }
            // Wait for CAPs.
            if (!Client.Network.CurrentSim.Caps.IsEventQueueRunning)
            {
                var EventQueueRunningEvent = new AutoResetEvent(false);
                EventHandler<EventQueueRunningEventArgs> handler = (sender, e) => { EventQueueRunningEvent.Set(); };
                Client.Network.EventQueueRunning += handler;
                EventQueueRunningEvent.WaitOne((int)millisecondsTimeout, true);
                Client.Network.EventQueueRunning -= handler;
            }
            Locks.ClientInstanceNetworkLock.ExitReadLock();

            Locks.ClientInstanceInventoryLock.EnterWriteLock();
            directUpdateInventoryRecursive(Client, root, millisecondsTimeout, force);
            Locks.ClientInstanceInventoryLock.ExitWriteLock();
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Converts Linden item permissions to a formatted string:
        ///     CDEMVT - Copy, Damage, Export, Modify, Move, Transfer
        ///     BBBBBBEEEEEEGGGGGGNNNNNNOOOOOO - Base, Everyone, Group, Next, Owner
        /// </summary>
        /// <param name="permissions">the item permissions</param>
        /// <returns>the literal permissions for an item</returns>
        public static string wasPermissionsToString(Permissions permissions)
        {
            Func<PermissionMask, string> segment = o =>
            {
                var seg = new StringBuilder();

                switch (!((uint)o & (uint)PermissionMask.Copy).Equals(0))
                {
                    case true:
                        seg.Append("c");
                        break;

                    default:
                        seg.Append("-");
                        break;
                }

                switch (!((uint)o & (uint)PermissionMask.Damage).Equals(0))
                {
                    case true:
                        seg.Append("d");
                        break;

                    default:
                        seg.Append("-");
                        break;
                }

                switch (!((uint)o & (uint)PermissionMask.Export).Equals(0))
                {
                    case true:
                        seg.Append("e");
                        break;

                    default:
                        seg.Append("-");
                        break;
                }

                switch (!((uint)o & (uint)PermissionMask.Modify).Equals(0))
                {
                    case true:
                        seg.Append("m");
                        break;

                    default:
                        seg.Append("-");
                        break;
                }

                switch (!((uint)o & (uint)PermissionMask.Move).Equals(0))
                {
                    case true:
                        seg.Append("v");
                        break;

                    default:
                        seg.Append("-");
                        break;
                }

                switch (!((uint)o & (uint)PermissionMask.Transfer).Equals(0))
                {
                    case true:
                        seg.Append("t");
                        break;

                    default:
                        seg.Append("-");
                        break;
                }

                return seg.ToString();
            };

            var x = new StringBuilder();
            x.Append(segment(permissions.BaseMask));
            x.Append(segment(permissions.EveryoneMask));
            x.Append(segment(permissions.GroupMask));
            x.Append(segment(permissions.NextOwnerMask));
            x.Append(segment(permissions.OwnerMask));
            return x.ToString();
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Converts a formatted string to item permissions:
        ///     CDEMVT - Copy, Damage, Export, Modify, Move, Transfer
        ///     BBBBBBEEEEEEGGGGGGNNNNNNOOOOOO - Base, Everyone, Group, Next, Owner
        /// </summary>
        /// <param name="permissions">the item permissions</param>
        /// <returns>the permissions for an item</returns>
        public static Permissions wasStringToPermissions(string permissions)
        {
            if (string.IsNullOrEmpty(permissions) || !permissions.Length.Equals(30))
                return Permissions.NoPermissions;

            Func<string, uint> segment = o =>
            {
                uint r = 0;
                switch (!char.ToLower(o[0]).Equals('c'))
                {
                    case false:
                        r |= (uint)PermissionMask.Copy;
                        break;
                }

                switch (!char.ToLower(o[1]).Equals('d'))
                {
                    case false:
                        r |= (uint)PermissionMask.Damage;
                        break;
                }

                switch (!char.ToLower(o[2]).Equals('e'))
                {
                    case false:
                        r |= (uint)PermissionMask.Export;
                        break;
                }

                switch (!char.ToLower(o[3]).Equals('m'))
                {
                    case false:
                        r |= (uint)PermissionMask.Modify;
                        break;
                }

                switch (!char.ToLower(o[4]).Equals('v'))
                {
                    case false:
                        r |= (uint)PermissionMask.Move;
                        break;
                }

                switch (!char.ToLower(o[5]).Equals('t'))
                {
                    case false:
                        r |= (uint)PermissionMask.Transfer;
                        break;
                }

                return r;
            };

            return new Permissions(segment(permissions.Substring(0, 6)),
                segment(permissions.Substring(6, 6)), segment(permissions.Substring(12, 6)),
                segment(permissions.Substring(18, 6)), segment(permissions.Substring(24, 6)));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Converts a formatted string to item permissions:
        ///     CDEMVT - Copy, Damage, Export, Modify, Move, Transfer
        ///     BBBBBBEEEEEEGGGGGGNNNNNNOOOOOO - Base, Everyone, Group, Next, Owner
        /// </summary>
        /// <param name="permissions">the item permissions</param>
        /// <returns>the permissions for an item</returns>
        public static bool wasStringToPermissions(string permissions, out Permissions p)
        {
            if (string.IsNullOrEmpty(permissions) || !permissions.Length.Equals(30))
            {
                p = Permissions.NoPermissions;
                return false;
            }

            Func<string, uint> segment = o =>
            {
                uint r = 0;
                switch (!char.ToLower(o[0]).Equals('c'))
                {
                    case false:
                        r |= (uint)PermissionMask.Copy;
                        break;
                }

                switch (!char.ToLower(o[1]).Equals('d'))
                {
                    case false:
                        r |= (uint)PermissionMask.Damage;
                        break;
                }

                switch (!char.ToLower(o[2]).Equals('e'))
                {
                    case false:
                        r |= (uint)PermissionMask.Export;
                        break;
                }

                switch (!char.ToLower(o[3]).Equals('m'))
                {
                    case false:
                        r |= (uint)PermissionMask.Modify;
                        break;
                }

                switch (!char.ToLower(o[4]).Equals('v'))
                {
                    case false:
                        r |= (uint)PermissionMask.Move;
                        break;
                }

                switch (!char.ToLower(o[5]).Equals('t'))
                {
                    case false:
                        r |= (uint)PermissionMask.Transfer;
                        break;
                }

                return r;
            };

            p = new Permissions(segment(permissions.Substring(0, 6)),
                segment(permissions.Substring(6, 6)), segment(permissions.Substring(12, 6)),
                segment(permissions.Substring(18, 6)), segment(permissions.Substring(24, 6)));

            return true;
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2015 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Returns an specific inventory item from an inventory base.
        /// </summary>
        /// <param name="inventoryBase">the inventory base</param>
        /// <returns>a specific inventory item or null</returns>
        public static object ToInventory(this InventoryBase inventoryBase)
        {
            if (inventoryBase is InventoryFolder)
            {
                return inventoryBase as InventoryFolder;
            }

            if (!(inventoryBase is InventoryItem)) return null;

            var inventoryItem = inventoryBase as InventoryItem;

            if (inventoryItem is InventoryWearable)
            {
                return inventoryItem as InventoryWearable;
            }

            if (inventoryItem is InventoryTexture)
            {
                return inventoryItem as InventoryTexture;
            }

            if (inventoryItem is InventorySound)
            {
                return inventoryItem as InventorySound;
            }

            if (inventoryItem is InventoryCallingCard)
            {
                return inventoryItem as InventoryCallingCard;
            }

            if (inventoryItem is InventoryLandmark)
            {
                return inventoryItem as InventoryLandmark;
            }

            if (inventoryItem is InventoryObject)
            {
                return inventoryItem as InventoryObject;
            }

            if (inventoryItem is InventoryNotecard)
            {
                return inventoryItem as InventoryNotecard;
            }

            if (inventoryItem is InventoryCategory)
            {
                return inventoryItem as InventoryCategory;
            }

            if (inventoryItem is InventoryLSL)
            {
                return inventoryItem as InventoryLSL;
            }

            if (inventoryItem is InventorySnapshot)
            {
                return inventoryItem as InventorySnapshot;
            }

            if (inventoryItem is InventoryAttachment)
            {
                return inventoryItem as InventoryAttachment;
            }

            if (inventoryItem is InventoryAnimation)
            {
                return inventoryItem as InventoryAnimation;
            }

            if (inventoryItem is InventoryGesture)
            {
                return inventoryItem as InventoryGesture;
            }

            return null;
        }

        /// <summary>
        ///     A wrapper that locks the inventory in order to retrieve folder contents.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="folder">the folder UUID</param>
        /// <param name="owner">the UUID of the owner of the folder</param>
        /// <param name="folders">whether to retrieive folders</param>
        /// <param name="items">whether to retrieve items</param>
        /// <param name="sortOrder">the sort order to return</param>
        /// <param name="millisecondTimeout">the timeout for the request in milliseconds</param>
        /// <returns>a list of inventory items contained in the folder</returns>
        public static List<InventoryBase> FolderContents(GridClient Client, UUID folder, UUID owner, bool folders,
            bool items,
            InventorySortOrder sortOrder, int millisecondTimeout)
        {
            Locks.ClientInstanceInventoryLock.EnterReadLock();
            var inventoryItems = Client.Inventory.FolderContents(folder, owner, folders, items, sortOrder, millisecondTimeout);
            Locks.ClientInstanceInventoryLock.ExitReadLock();
            return inventoryItems;
        }
    }
}
