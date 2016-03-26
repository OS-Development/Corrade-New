///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> changeappearance =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Grooming))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }

                    string folder =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FOLDER)),
                                corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(folder))
                    {
                        throw new ScriptException(ScriptError.NO_FOLDER_SPECIFIED);
                    }

                    InventoryFolder inventoryFolder;
                    lock (Locks.ClientInstanceInventoryLock)
                    {
                        inventoryFolder =
                            Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode, folder)
                                .FirstOrDefault(o => o is InventoryFolder) as InventoryFolder;
                    }
                    if (inventoryFolder == null)
                    {
                        throw new ScriptException(ScriptError.FOLDER_NOT_FOUND);
                    }

                    List<InventoryItem> equipItems = new List<InventoryItem>();
                    lock (Locks.ClientInstanceInventoryLock)
                    {
                        equipItems.AddRange(
                            Client.Inventory.Store.GetContents(inventoryFolder)
                                .Select(o => Inventory.ResolveItemLink(Client, o as InventoryItem))
                                .Where(Inventory.CanBeWorn));
                    }
                    // Check if any items are left over.
                    if (!equipItems.Any())
                    {
                        throw new ScriptException(ScriptError.NO_EQUIPABLE_ITEMS);
                    }

                    // stop non default animations if requested
                    bool deanimate;
                    switch (bool.TryParse(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DEANIMATE)),
                            corradeCommandParameters.Message)), out deanimate) && deanimate)
                    {
                        case true:
                            // stop all non-built-in animations
                            Client.Self.Animate(new Dictionary<UUID, bool>(typeof (Animations).GetFields(
                                BindingFlags.Public |
                                BindingFlags.Static)
                                .AsParallel()
                                .Select(o => new {k = (UUID) o.GetValue(null), v = false})
                                .ToDictionary(o => o.k, o => o.v)), true);
                            break;
                    }

                    // Create a list of links that should be removed.
                    List<UUID> removeItems = new List<UUID>();
                    object LockObject = new object();
                    Parallel.ForEach(
                        Inventory.GetCurrentOutfitFolderLinks(Client, CurrentOutfitFolder), o =>
                        {
                            switch (Inventory.IsBodyPart(Client, o))
                            {
                                case true:
                                    if (
                                        equipItems.AsParallel()
                                            .Where(t => Inventory.IsBodyPart(Client, t))
                                            .Any(
                                                p =>
                                                    ((InventoryWearable) p).WearableType.Equals(
                                                        ((InventoryWearable) Inventory.ResolveItemLink(Client, o))
                                                            .WearableType)))
                                        goto default;
                                    break;
                                default:
                                    lock (LockObject)
                                    {
                                        removeItems.Add(o.UUID);
                                    }
                                    break;
                            }
                        });

                    lock (Locks.ClientInstanceInventoryLock)
                    {
                        // Now remove the links.
                        Client.Inventory.Remove(removeItems, null);

                        // Add links to new items.
                        foreach (InventoryItem item in equipItems)
                        {
                            Inventory.AddLink(Client, item, CurrentOutfitFolder);
                        }
                    }

                    // And replace the outfit wit hthe new items.
                    lock (Locks.ClientInstanceAppearanceLock)
                    {
                        Client.Appearance.ReplaceOutfit(equipItems, false);
                    }

                    // Update inventory.
                    try
                    {
                        Inventory.UpdateInventoryRecursive(Client, CurrentOutfitFolder,
                            corradeConfiguration.ServicesTimeout);
                    }
                    catch (Exception)
                    {
                        Feedback(Reflection.GetDescriptionFromEnumValue(ConsoleError.ERROR_UPDATING_INVENTORY));
                    }

                    // Schedule a rebake.
                    RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                };
        }
    }
}