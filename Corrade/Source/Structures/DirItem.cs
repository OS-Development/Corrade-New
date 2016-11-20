///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using System.Reflection;
using System.Threading;
using Corrade.Constants;
using OpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade.Structures
{
    /// <summary>
    ///     An inventory item.
    /// </summary>
    public struct DirItem
    {
        [Reflection.NameAttribute("item")] public UUID Item;
        [Reflection.NameAttribute("name")] public string Name;
        [Reflection.NameAttribute("permissions")] public string Permissions;
        [Reflection.NameAttribute("type")] public Enumerations.DirItemType Type;
        [Reflection.NameAttribute("time")] public DateTime Time;

        public static DirItem FromInventoryBase(GridClient Client, InventoryBase inventoryBase, uint millisecondsTimeout)
        {
            var item = new DirItem
            {
                Name = inventoryBase.Name,
                Item = inventoryBase.UUID,
                Permissions = CORRADE_CONSTANTS.PERMISSIONS.NONE
            };

            if (inventoryBase is InventoryFolder)
            {
                item.Type = Enumerations.DirItemType.FOLDER;
                var folder = inventoryBase as InventoryFolder;
                var FolderUpdatedEvent = new ManualResetEvent(false);
                EventHandler<FolderUpdatedEventArgs> FolderUpdatedEventHandler = (p, q) =>
                {
                    // Enqueue all the new folders.
                    var firstInventoryBase = Client.Inventory.Store.GetContents(q.FolderID)
                        .AsParallel()
                        .FirstOrDefault(r => r is InventoryItem);

                    if (firstInventoryBase is InventoryItem)
                        item.Time = (firstInventoryBase as InventoryItem).CreationDate;
                    FolderUpdatedEvent.Set();
                };
                Client.Inventory.FolderUpdated += FolderUpdatedEventHandler;
                FolderUpdatedEvent.Reset();
                Client.Inventory.RequestFolderContents(folder.UUID, Client.Self.AgentID, true, true,
                    InventorySortOrder.ByDate);
                FolderUpdatedEvent.WaitOne((int) millisecondsTimeout, false);
                Client.Inventory.FolderUpdated -= FolderUpdatedEventHandler;
                return item;
            }

            if (!(inventoryBase is InventoryItem)) return item;

            var inventoryItem = inventoryBase as InventoryItem;
            item.Permissions = Inventory.wasPermissionsToString(inventoryItem.Permissions);
            item.Time = inventoryItem.CreationDate;

            if (inventoryItem is InventoryWearable)
            {
                item.Type =
                    (Enumerations.DirItemType) typeof(Enumerations.DirItemType).GetFields(BindingFlags.Public |
                                                                                          BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                Strings.StringEquals(o.Name,
                                    Enum.GetName(typeof(WearableType),
                                        (inventoryItem as InventoryWearable).WearableType),
                                    StringComparison.OrdinalIgnoreCase)).GetValue(null);
                return item;
            }

            if (inventoryItem is InventoryTexture)
            {
                item.Type = Enumerations.DirItemType.TEXTURE;
                return item;
            }

            if (inventoryItem is InventorySound)
            {
                item.Type = Enumerations.DirItemType.SOUND;
                return item;
            }

            if (inventoryItem is InventoryCallingCard)
            {
                item.Type = Enumerations.DirItemType.CALLINGCARD;
                return item;
            }

            if (inventoryItem is InventoryLandmark)
            {
                item.Type = Enumerations.DirItemType.LANDMARK;
                return item;
            }

            if (inventoryItem is InventoryObject)
            {
                item.Type = Enumerations.DirItemType.OBJECT;
                return item;
            }

            if (inventoryItem is InventoryNotecard)
            {
                item.Type = Enumerations.DirItemType.NOTECARD;
                return item;
            }

            if (inventoryItem is InventoryCategory)
            {
                item.Type = Enumerations.DirItemType.CATEGORY;
                return item;
            }

            if (inventoryItem is InventoryLSL)
            {
                item.Type = Enumerations.DirItemType.LSL;
                return item;
            }

            if (inventoryItem is InventorySnapshot)
            {
                item.Type = Enumerations.DirItemType.SNAPSHOT;
                return item;
            }

            if (inventoryItem is InventoryAttachment)
            {
                item.Type = Enumerations.DirItemType.ATTACHMENT;
                return item;
            }

            if (inventoryItem is InventoryAnimation)
            {
                item.Type = Enumerations.DirItemType.ANIMATION;
                return item;
            }

            if (inventoryItem is InventoryGesture)
            {
                item.Type = Enumerations.DirItemType.GESTURE;
                return item;
            }

            item.Type = Enumerations.DirItemType.NONE;
            return item;
        }
    }
}