///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
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
            return item.IsLink() && Client.Inventory.Store.Contains(item.AssetUUID) &&
                   Client.Inventory.Store[item.AssetUUID] is InventoryItem
                ? (InventoryItem) Client.Inventory.Store[item.AssetUUID]
                : item;
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
            return Client.Inventory.Store.GetContents(outfitFolder)
                .AsParallel()
                .Where(o => CanBeWorn(o) && ((InventoryItem) o).AssetType == AssetType.Link)
                .Select(o => o as InventoryItem);
        }

        public static void Attach(GridClient Client, InventoryFolder CurrentOutfitFolder, InventoryItem item,
            AttachmentPoint point, bool replace, uint millisecondsTimeout)
        {
            lock (Locks.ClientInstanceInventoryLock)
            {
                InventoryItem realItem = ResolveItemLink(Client, item);
                if (!(realItem is InventoryAttachment) && !(realItem is InventoryObject)) return;
                Client.Appearance.Attach(realItem, point, replace);
                AddLink(Client, realItem, CurrentOutfitFolder);
                UpdateInventoryRecursive(Client, CurrentOutfitFolder, millisecondsTimeout);
            }
        }

        public static void Detach(GridClient Client, InventoryFolder CurrentOutfitFolder, InventoryItem item,
            uint millisecondsTimeout)
        {
            lock (Locks.ClientInstanceInventoryLock)
            {
                InventoryItem realItem = ResolveItemLink(Client, item);
                if (!(realItem is InventoryAttachment) && !(realItem is InventoryObject)) return;
                RemoveLink(Client, realItem, CurrentOutfitFolder);
                Client.Appearance.Detach(realItem);
                UpdateInventoryRecursive(Client, CurrentOutfitFolder, millisecondsTimeout);
            }
        }

        public static void Wear(GridClient Client, InventoryFolder CurrentOutfitFolder, InventoryItem item, bool replace,
            uint millisecondsTimeout)
        {
            lock (Locks.ClientInstanceInventoryLock)
            {
                InventoryItem realItem = ResolveItemLink(Client, item);
                if (!(realItem is InventoryWearable)) return;
                Client.Appearance.AddToOutfit(realItem, replace);
                AddLink(Client, realItem, CurrentOutfitFolder);
                UpdateInventoryRecursive(Client, CurrentOutfitFolder, millisecondsTimeout);
            }
        }

        public static void UnWear(GridClient Client, InventoryFolder CurrentOutfitFolder, InventoryItem item,
            uint millisecondsTimeout)
        {
            lock (Locks.ClientInstanceInventoryLock)
            {
                InventoryItem realItem = ResolveItemLink(Client, item);
                if (!(realItem is InventoryWearable)) return;
                Client.Appearance.RemoveFromOutfit(realItem);
                RemoveLink(Client, realItem, CurrentOutfitFolder);
                UpdateInventoryRecursive(Client, CurrentOutfitFolder, millisecondsTimeout);
            }
        }

        /// <summary>
        ///     Is the item a body part?
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="item">the item to check</param>
        /// <returns>true if the item is a body part</returns>
        public static bool IsBodyPart(GridClient Client, InventoryItem item)
        {
            InventoryItem realItem = ResolveItemLink(Client, item);
            if (!(realItem is InventoryWearable)) return false;
            WearableType t = ((InventoryWearable) realItem).WearableType;
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

            if (!GetCurrentOutfitFolderLinks(Client, outfitFolder).AsParallel().Any(o => o.AssetUUID.Equals(item.UUID)))
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

        /// <summary>
        ///     Remove current outfit folder links for multiple specified inventory item.
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="item">the item whose link should be removed</param>
        /// <param name="outfitFolder">the outfit folder</param>
        private static void RemoveLink(GridClient Client, InventoryItem item, InventoryFolder outfitFolder)
        {
            if (outfitFolder == null) return;

            Client.Inventory.Remove(
                GetCurrentOutfitFolderLinks(Client, outfitFolder)
                    .AsParallel()
                    .Where(o => o.AssetUUID.Equals(item.UUID))
                    .Select(o => o.UUID)
                    .ToList(), null);
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
            HashSet<Primitive> selectedPrimitives =
                new HashSet<Primitive>(Client.Network.CurrentSim.ObjectsPrimitives.Copy()
                    .Values
                    .AsParallel()
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
        /// <returns>a list of items matching the item name</returns>
        public static IEnumerable<T> FindInventory<T>(GridClient Client, InventoryNode root, object criteria)
        {
            if ((criteria is Regex && (criteria as Regex).IsMatch(root.Data.Name)) ||
                (criteria is string &&
                 (criteria as string).Equals(root.Data.Name, StringComparison.Ordinal)) ||
                (criteria is UUID &&
                 (criteria.Equals(root.Data.UUID) ||
                  (Client.Inventory.Store[root.Data.UUID] is InventoryItem &&
                   (Client.Inventory.Store[root.Data.UUID] as InventoryItem).AssetUUID.Equals(criteria)))))
            {
                if (typeof (T) == typeof (InventoryNode))
                {
                    yield return (T) (object) root;
                }
                if (typeof (T) == typeof (InventoryBase))
                {
                    yield return (T) (object) Client.Inventory.Store[root.Data.UUID];
                }
            }
            foreach (
                T item in root.Nodes.Values.AsParallel().SelectMany(node => FindInventory<T>(Client, node, criteria)))
            {
                yield return item;
            }
        }

        ///////////////////////////////////////////////////////////////////////////
        //    Copyright (C) 2014 Wizardry and Steamworks - License: GNU GPLv3    //
        ///////////////////////////////////////////////////////////////////////////
        /// <summary>
        ///     Fetches items and their full path from the inventory starting with
        ///     an inventory node where the search criteria finds:
        ///     - name as string
        ///     - name as Regex
        ///     - UUID as UUID
        /// </summary>
        /// <param name="Client">the grid client to use</param>
        /// <param name="root">the node to start the search from</param>
        /// <param name="criteria">the name, UUID or Regex of the item to be found</param>
        /// <param name="prefix">any prefix to append to the found paths</param>
        /// <returns>items matching criteria and their full inventoy path</returns>
        public static IEnumerable<KeyValuePair<T, LinkedList<string>>> FindInventoryPath<T>(GridClient Client,
            InventoryNode root, object criteria, LinkedList<string> prefix)
        {
            if ((criteria is Regex && (criteria as Regex).IsMatch(root.Data.Name)) ||
                (criteria is string &&
                 (criteria as string).Equals(root.Data.Name, StringComparison.Ordinal)) ||
                (criteria is UUID &&
                 (criteria.Equals(root.Data.UUID) ||
                  (Client.Inventory.Store[root.Data.UUID] is InventoryItem &&
                   (Client.Inventory.Store[root.Data.UUID] as InventoryItem).AssetUUID.Equals(criteria)))))
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
                KeyValuePair<T, LinkedList<string>> o in
                    root.Nodes.Values.AsParallel()
                        .SelectMany(o => FindInventoryPath<T>(Client, o, criteria, new LinkedList<string>(
                            prefix.Concat(new[] {root.Data.Name})))))
            {
                yield return o;
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
            // Create the queue of folders.
            BlockingQueue<InventoryFolder> inventoryFolders = new BlockingQueue<InventoryFolder>();
            // Enqueue the first folder (root).
            inventoryFolders.Enqueue(o);

            AutoResetEvent FolderUpdatedEvent = new AutoResetEvent(false);
            EventHandler<FolderUpdatedEventArgs> FolderUpdatedEventHandler = (p, q) =>
            {
                // Enqueue all the new folders.
                Client.Inventory.Store.GetContents(q.FolderID).AsParallel().Where(r => r is InventoryFolder).ForAll(r =>
                {
                    inventoryFolders.Enqueue(r as InventoryFolder);
                });
                FolderUpdatedEvent.Set();
            };

            do
            {
                InventoryFolder folder = inventoryFolders.Dequeue();
                if (folder == null) continue;
                Client.Inventory.FolderUpdated += FolderUpdatedEventHandler;
                Client.Inventory.RequestFolderContents(folder.UUID, Client.Self.AgentID, true, true,
                    InventorySortOrder.ByDate);
                FolderUpdatedEvent.WaitOne((int) millisecondsTimeout, false);
                Client.Inventory.FolderUpdated -= FolderUpdatedEventHandler;
            } while (!inventoryFolders.Count.Equals(0));
        }
    }
}