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
            lock (Locks.ClientInstanceInventoryLock)
            {
                return item.IsLink() && Client.Inventory.Store.Contains(item.AssetUUID) &&
                       Client.Inventory.Store[item.AssetUUID] is InventoryItem
                    ? (InventoryItem) Client.Inventory.Store[item.AssetUUID]
                    : item;
            }
        }

        /// <summary>
        ///     Get current outfit folder links.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="outfitFolder">the outfit folder to return items from</param>
        /// <returns>a list of inventory items that can be part of appearance (attachments, wearables)</returns>
        public static IEnumerable<InventoryItem> GetCurrentOutfitFolderLinks(GridClient Client,
            InventoryFolder outfitFolder)
        {
            lock (Locks.ClientInstanceInventoryLock)
            {
                return Client.Inventory.Store.GetContents(outfitFolder)
                    .AsParallel()
                    .Where(o => CanBeWorn(o) && ((InventoryItem) o).AssetType == AssetType.Link)
                    .Select(o => o as InventoryItem);
            }
        }

        public static void Attach(GridClient Client, InventoryFolder CurrentOutfitFolder, InventoryItem item,
            AttachmentPoint point, bool replace, uint millisecondsTimeout)
        {
            var realItem = ResolveItemLink(Client, item);
            if (!(realItem is InventoryAttachment) && !(realItem is InventoryObject)) return;
            lock (Locks.ClientInstanceAppearanceLock)
            {
                Client.Appearance.Attach(realItem, point, replace);
            }
            AddLink(Client, realItem, CurrentOutfitFolder);
            UpdateInventoryRecursive(Client, CurrentOutfitFolder, millisecondsTimeout);
        }

        public static void Detach(GridClient Client, InventoryFolder CurrentOutfitFolder, InventoryItem item,
            uint millisecondsTimeout)
        {
            var realItem = ResolveItemLink(Client, item);
            if (!(realItem is InventoryAttachment) && !(realItem is InventoryObject)) return;
            RemoveLink(Client, realItem, CurrentOutfitFolder);
            lock (Locks.ClientInstanceAppearanceLock)
            {
                Client.Appearance.Detach(realItem);
            }
            UpdateInventoryRecursive(Client, CurrentOutfitFolder, millisecondsTimeout);
        }

        public static void Wear(GridClient Client, InventoryFolder CurrentOutfitFolder, InventoryItem item, bool replace,
            uint millisecondsTimeout)
        {
            var realItem = ResolveItemLink(Client, item);
            if (!(realItem is InventoryWearable)) return;
            lock (Locks.ClientInstanceAppearanceLock)
            {
                Client.Appearance.AddToOutfit(realItem, replace);
            }
            AddLink(Client, realItem, CurrentOutfitFolder);
            UpdateInventoryRecursive(Client, CurrentOutfitFolder, millisecondsTimeout);
        }

        public static void UnWear(GridClient Client, InventoryFolder CurrentOutfitFolder, InventoryItem item,
            uint millisecondsTimeout)
        {
            var realItem = ResolveItemLink(Client, item);
            if (!(realItem is InventoryWearable)) return;
            lock (Locks.ClientInstanceAppearanceLock)
            {
                Client.Appearance.RemoveFromOutfit(realItem);
            }
            RemoveLink(Client, realItem, CurrentOutfitFolder);
            UpdateInventoryRecursive(Client, CurrentOutfitFolder, millisecondsTimeout);
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
            var t = ((InventoryWearable) realItem).WearableType;
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
        public static void AddLink(GridClient Client, InventoryItem item, InventoryFolder outfitFolder)
        {
            if (outfitFolder == null) return;

            var contents = new List<InventoryItem>(GetCurrentOutfitFolderLinks(Client, outfitFolder));
            if (!contents.AsParallel().Any(o => o.AssetUUID.Equals(item.UUID)))
            {
                lock (Locks.ClientInstanceInventoryLock)
                {
                    Client.Inventory.CreateLink(outfitFolder.UUID, item.UUID, item.Name,
                        item.InventoryType.Equals(InventoryType.Wearable) && !IsBodyPart(Client, item)
                            ? $"@{(int) ((InventoryWearable) item).WearableType}{0:00}"
                            : string.Empty, AssetType.Link, item.InventoryType, UUID.Random(), (success, newItem) =>
                            {
                                if (success)
                                {
                                    Client.Inventory.RequestFetchInventory(newItem.UUID, newItem.OwnerID);
                                }
                            });
                }
            }
        }

        /// <summary>
        ///     Remove current outfit folder links for multiple specified inventory item.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="item">the item whose link should be removed</param>
        /// <param name="outfitFolder">the outfit folder</param>
        private static void RemoveLink(GridClient Client, InventoryItem item, InventoryFolder outfitFolder)
        {
            if (outfitFolder == null) return;

            var contents = new List<InventoryItem>(GetCurrentOutfitFolderLinks(Client, outfitFolder));
            lock (Locks.ClientInstanceInventoryLock)
            {
                Client.Inventory.Remove(
                    contents
                        .AsParallel()
                        .Where(o => o.AssetUUID.Equals(item.UUID))
                        .Select(o => o.UUID)
                        .ToList(), null);
            }
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
            var selectedPrimitives =
                new HashSet<Primitive>(Client.Network.Simulators.AsParallel()
                    .Select(o => o.ObjectsPrimitives)
                    .Select(o => o.Copy().Values)
                    .SelectMany(o => o)
                    .Where(o => o.ParentID.Equals(Client.Self.LocalID)));
            if (!selectedPrimitives.Any() || !Services.UpdatePrimitives(Client, ref selectedPrimitives, dataTimeout))
                return Enumerable.Empty<KeyValuePair<Primitive, AttachmentPoint>>();
            return selectedPrimitives
                .AsParallel()
                .Select(o => new KeyValuePair<Primitive, AttachmentPoint>(o,
                    (AttachmentPoint) (((o.PrimData.State & 0xF0) >> 4) |
                                       ((o.PrimData.State & ~0xF0) << 4))));
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Gets the inventory wearables that are currently being worn.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="outfitFolder">the folder to start the search from</param>
        /// <returns>the worn inventory itemse</returns>
        public static IEnumerable<InventoryItem> GetWearables(GridClient Client, InventoryFolder outfitFolder)
        {
            return outfitFolder != null
                ? GetCurrentOutfitFolderLinks(Client, outfitFolder)
                    .AsParallel()
                    .Select(o => ResolveItemLink(Client, o))
                    .Where(o => o is InventoryWearable)
                    .Select(o => o)
                : Enumerable.Empty<InventoryItem>();
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
        private static IEnumerable<T> directFindInventory<T>(GridClient Client, InventoryNode root, string criteria,
            uint millisecondsTimeout)
        {
            var rootFolder = Client.Inventory.Store[root.Data.UUID] as InventoryFolder;
            if (rootFolder == null)
                yield break;
            var inventoryFolders = new HashSet<InventoryFolder>();
            foreach (var item in Client.Inventory.Store.GetContents(rootFolder))
            {
                if (string.Equals(criteria, item.Name, StringComparison.Ordinal))
                {
                    if (typeof (T) == typeof (InventoryNode))
                    {
                        yield return (T) (object) Client.Inventory.Store.GetNodeFor(item.UUID);
                    }
                    if (typeof (T) == typeof (InventoryBase))
                    {
                        yield return (T) (object) item;
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
                    FolderUpdatedEvent.WaitOne((int) millisecondsTimeout, false);
                    Client.Inventory.FolderUpdated -= FolderUpdatedEventHandler;
                }
                foreach (var o in directFindInventory<T>(Client, folderNode, criteria, millisecondsTimeout))
                {
                    yield return o;
                }
            }
        }

        public static IEnumerable<T> FindInventory<T>(GridClient Client, InventoryNode root, string criteria,
            uint millisecondsTimeout)
        {
            lock (Locks.ClientInstanceInventoryLock)
            {
                return directFindInventory<T>(Client, root, criteria, millisecondsTimeout);
            }
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
        private static IEnumerable<T> directFindInventory<T>(GridClient Client, InventoryNode root, UUID criteria,
            uint millisecondsTimeout)
        {
            var rootFolder = Client.Inventory.Store[root.Data.UUID] as InventoryFolder;
            if (rootFolder == null)
                yield break;
            var inventoryFolders = new HashSet<InventoryFolder>();
            foreach (var item in Client.Inventory.Store.GetContents(rootFolder))
            {
                if (criteria.Equals(item.UUID))
                {
                    if (typeof (T) == typeof (InventoryNode))
                    {
                        yield return (T) (object) Client.Inventory.Store.GetNodeFor(item.UUID);
                    }
                    if (typeof (T) == typeof (InventoryBase))
                    {
                        yield return (T) (object) item;
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
                    FolderUpdatedEvent.WaitOne((int) millisecondsTimeout, false);
                    Client.Inventory.FolderUpdated -= FolderUpdatedEventHandler;
                }
                foreach (var o in directFindInventory<T>(Client, folderNode, criteria, millisecondsTimeout))
                {
                    yield return o;
                }
            }
        }

        public static IEnumerable<T> FindInventory<T>(GridClient Client, InventoryNode root, UUID criteria,
            uint millisecondsTimeout)
        {
            lock (Locks.ClientInstanceInventoryLock)
            {
                return directFindInventory<T>(Client, root, criteria, millisecondsTimeout);
            }
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
                    if (typeof (T) == typeof (InventoryNode))
                    {
                        yield return (T) (object) Client.Inventory.Store.GetNodeFor(item.UUID);
                    }
                    if (typeof (T) == typeof (InventoryBase))
                    {
                        yield return (T) (object) item;
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
                    FolderUpdatedEvent.WaitOne((int) millisecondsTimeout, false);
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
            lock (Locks.ClientInstanceInventoryLock)
            {
                return directFindInventory<T>(Client, root, criteria, millisecondsTimeout);
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Fetches items and their full path from the inventory starting with
        ///     an inventory node where the search criteria is a string.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="root">the node to start the search from</param>
        /// <param name="criteria">the name of the item to find</param>
        /// <param name="prefix">any prefix to append to the found paths</param>
        /// <returns>items matching criteria and their full inventoy path</returns>
        private static IEnumerable<KeyValuePair<T, LinkedList<string>>> directFindInventoryPath<T>(GridClient Client,
            InventoryNode root, string criteria, LinkedList<string> prefix)
        {
            if (string.Equals(criteria, root.Data.Name, StringComparison.Ordinal))
            {
                if (typeof (T) == typeof (InventoryBase))
                {
                    yield return
                        new KeyValuePair<T, LinkedList<string>>((T) (object) Client.Inventory.Store[root.Data.UUID],
                            new LinkedList<string>(
                                prefix.Concat(new[] {root.Data.Name})));
                }
                if (typeof (T) == typeof (InventoryNode))
                {
                    yield return
                        new KeyValuePair<T, LinkedList<string>>((T) (object) root,
                            new LinkedList<string>(
                                prefix.Concat(new[] {root.Data.Name})));
                }
            }
            foreach (
                var o in
                    root.Nodes.Values.AsParallel()
                        .SelectMany(o => directFindInventoryPath<T>(Client, o, criteria, new LinkedList<string>(
                            prefix.Concat(new[] {root.Data.Name})))))
            {
                yield return o;
            }
        }

        public static IEnumerable<KeyValuePair<T, LinkedList<string>>> FindInventoryPath<T>(GridClient Client,
            InventoryNode root, string criteria, LinkedList<string> prefix)
        {
            lock (Locks.ClientInstanceInventoryLock)
            {
                return directFindInventoryPath<T>(Client, root, criteria, prefix);
            }
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
                if (typeof (T) == typeof (InventoryBase))
                {
                    yield return
                        new KeyValuePair<T, LinkedList<string>>((T) (object) Client.Inventory.Store[root.Data.UUID],
                            new LinkedList<string>(
                                prefix.Concat(new[] {root.Data.Name})));
                }
                if (typeof (T) == typeof (InventoryNode))
                {
                    yield return
                        new KeyValuePair<T, LinkedList<string>>((T) (object) root,
                            new LinkedList<string>(
                                prefix.Concat(new[] {root.Data.Name})));
                }
            }
            foreach (
                var o in
                    root.Nodes.Values.AsParallel()
                        .SelectMany(o => directFindInventoryPath<T>(Client, o, criteria, new LinkedList<string>(
                            prefix.Concat(new[] {root.Data.Name})))))
            {
                yield return o;
            }
        }

        public static IEnumerable<KeyValuePair<T, LinkedList<string>>> FindInventoryPath<T>(GridClient Client,
            InventoryNode root, UUID criteria, LinkedList<string> prefix)
        {
            lock (Locks.ClientInstanceInventoryLock)
            {
                return directFindInventoryPath<T>(Client, root, criteria, prefix);
            }
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
                if (typeof (T) == typeof (InventoryBase))
                {
                    yield return
                        new KeyValuePair<T, LinkedList<string>>((T) (object) Client.Inventory.Store[root.Data.UUID],
                            new LinkedList<string>(
                                prefix.Concat(new[] {root.Data.Name})));
                }
                if (typeof (T) == typeof (InventoryNode))
                {
                    yield return
                        new KeyValuePair<T, LinkedList<string>>((T) (object) root,
                            new LinkedList<string>(
                                prefix.Concat(new[] {root.Data.Name})));
                }
            }
            foreach (
                var o in
                    root.Nodes.Values.AsParallel()
                        .SelectMany(o => directFindInventoryPath<T>(Client, o, criteria, new LinkedList<string>(
                            prefix.Concat(new[] {root.Data.Name})))))
            {
                yield return o;
            }
        }

        public static IEnumerable<KeyValuePair<T, LinkedList<string>>> FindInventoryPath<T>(GridClient Client,
            InventoryNode root, Regex criteria, LinkedList<string> prefix)
        {
            lock (Locks.ClientInstanceInventoryLock)
            {
                return directFindInventoryPath<T>(Client, root, criteria, prefix);
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Updates the inventory starting from a folder recursively.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="o">the folder to use as the root</param>
        /// <param name="millisecondsTimeout">the timeout for each folder update in milliseconds</param>
        public static void UpdateInventoryRecursive(GridClient Client, InventoryFolder o, uint millisecondsTimeout)
        {
            // Check if CAPS is connected or wait a while.
            if (!Client.Network.CurrentSim.Caps.IsEventQueueRunning)
            {
                // Wait for CAPs.
                ManualResetEvent EventQueueRunningEvent = new ManualResetEvent(false);
                EventHandler<EventQueueRunningEventArgs> EventQueueRunningEventHandler = (p, q) =>
                {
                    EventQueueRunningEvent.Set();
                };
                Client.Network.EventQueueRunning += EventQueueRunningEventHandler;
                EventQueueRunningEvent.WaitOne((int)millisecondsTimeout, false);
                Client.Network.EventQueueRunning -= EventQueueRunningEventHandler;
            }

            // Create the queue of folders.
            var inventoryFolders = new BlockingQueue<InventoryFolder>();
            // Enqueue the first folder (root).
            inventoryFolders.Enqueue(o);

            var FolderUpdatedEvent = new ManualResetEvent(false);
            EventHandler<FolderUpdatedEventArgs> FolderUpdatedEventHandler = (p, q) =>
            {
                // Enqueue all the new folders.
                Client.Inventory.Store.GetContents(q.FolderID)
                    .AsParallel()
                    .Where(r => r is InventoryFolder)
                    .ForAll(r => { inventoryFolders.Enqueue(r as InventoryFolder); });
                FolderUpdatedEvent.Set();
            };

            do
            {
                // Dequeue folder.
                var folder = inventoryFolders.Dequeue();
                // Check if the node exists and if it does not need and update then continue.
                var node = Client.Inventory.Store.GetNodeFor(folder.UUID);
                if (node != null && !node.NeedsUpdate)
                    continue;
                lock (Locks.ClientInstanceInventoryLock)
                {
                    Client.Inventory.FolderUpdated += FolderUpdatedEventHandler;
                    FolderUpdatedEvent.Reset();
                    Client.Inventory.RequestFolderContents(folder.UUID, Client.Self.AgentID, true, true,
                        InventorySortOrder.ByDate);
                    FolderUpdatedEvent.WaitOne((int)millisecondsTimeout, false);
                    Client.Inventory.FolderUpdated -= FolderUpdatedEventHandler;
                }
            } while (inventoryFolders.Any());
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

                switch (!((uint) o & (uint) PermissionMask.Copy).Equals(0))
                {
                    case true:
                        seg.Append("c");
                        break;
                    default:
                        seg.Append("-");
                        break;
                }

                switch (!((uint) o & (uint) PermissionMask.Damage).Equals(0))
                {
                    case true:
                        seg.Append("d");
                        break;
                    default:
                        seg.Append("-");
                        break;
                }

                switch (!((uint) o & (uint) PermissionMask.Export).Equals(0))
                {
                    case true:
                        seg.Append("e");
                        break;
                    default:
                        seg.Append("-");
                        break;
                }

                switch (!((uint) o & (uint) PermissionMask.Modify).Equals(0))
                {
                    case true:
                        seg.Append("m");
                        break;
                    default:
                        seg.Append("-");
                        break;
                }

                switch (!((uint) o & (uint) PermissionMask.Move).Equals(0))
                {
                    case true:
                        seg.Append("v");
                        break;
                    default:
                        seg.Append("-");
                        break;
                }

                switch (!((uint) o & (uint) PermissionMask.Transfer).Equals(0))
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
            Func<string, uint> segment = o =>
            {
                uint r = 0;
                switch (!char.ToLower(o[0]).Equals('c'))
                {
                    case false:
                        r |= (uint) PermissionMask.Copy;
                        break;
                }

                switch (!char.ToLower(o[1]).Equals('d'))
                {
                    case false:
                        r |= (uint) PermissionMask.Damage;
                        break;
                }

                switch (!char.ToLower(o[2]).Equals('e'))
                {
                    case false:
                        r |= (uint) PermissionMask.Export;
                        break;
                }

                switch (!char.ToLower(o[3]).Equals('m'))
                {
                    case false:
                        r |= (uint) PermissionMask.Modify;
                        break;
                }

                switch (!char.ToLower(o[4]).Equals('v'))
                {
                    case false:
                        r |= (uint) PermissionMask.Move;
                        break;
                }

                switch (!char.ToLower(o[5]).Equals('t'))
                {
                    case false:
                        r |= (uint) PermissionMask.Transfer;
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
        ///     Sets permissions on inventory items using the [WaS] permission mask format.
        /// </summary>
        /// <param name="Client">the grid client to use to set permissions</param>
        /// <param name="inventoryItem">an inventory item</param>
        /// <param name="wasPermissions">the string permissions to set</param>
        /// <param name="millisecondsTimeout">the services timeout</param>
        /// <returns>true in case the permissions were set successfully</returns>
        public static bool wasSetInventoryItemPermissions(GridClient Client, InventoryItem inventoryItem,
            string wasPermissions, uint millisecondsTimeout)
        {
            // Update the object.
            var permissions = wasStringToPermissions(wasPermissions);
            inventoryItem.Permissions = permissions;
            lock (Locks.ClientInstanceInventoryLock)
            {
                Client.Inventory.RequestUpdateItem(inventoryItem);
            }
            // Now check that the permissions were set.
            var succeeded = false;
            var ItemReceivedEvent = new ManualResetEvent(false);
            EventHandler<ItemReceivedEventArgs> ItemReceivedEventHandler =
                (sender, args) =>
                {
                    if (!args.Item.UUID.Equals(inventoryItem.UUID)) return;
                    succeeded = args.Item.Permissions.Equals(permissions);
                    ItemReceivedEvent.Set();
                };
            lock (Locks.ClientInstanceInventoryLock)
            {
                Client.Inventory.ItemReceived += ItemReceivedEventHandler;
                Client.Inventory.RequestFetchInventory(inventoryItem.UUID, inventoryItem.OwnerID);
                if (
                    !ItemReceivedEvent.WaitOne((int) millisecondsTimeout, false))
                {
                    Client.Inventory.ItemReceived -= ItemReceivedEventHandler;
                    succeeded = false;
                }
                Client.Inventory.ItemReceived -= ItemReceivedEventHandler;
            }
            return succeeded;
        }
    }
}