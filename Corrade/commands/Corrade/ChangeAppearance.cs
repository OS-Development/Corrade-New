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
                        inventoryFolder = FindInventory<InventoryBase>(Client.Inventory.Store.RootNode, folder)
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
                                .Select(o => ResolveItemLink(o as InventoryItem))
                                .Where(CanBeWorn));
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
                            HashSet<UUID> lindenAnimations = new HashSet<UUID>(typeof (Animations).GetFields(
                                BindingFlags.Public |
                                BindingFlags.Static).AsParallel().Select(o => (UUID) o.GetValue(null)));
                            Parallel.ForEach(
                                Client.Self.SignaledAnimations.Copy()
                                    .Keys.AsParallel()
                                    .Where(o => !lindenAnimations.Contains(o)),
                                o => { Client.Self.AnimationStop(o, true); });
                            break;
                    }

                    // Create a list of links that should be removed.
                    List<UUID> removeItems = new List<UUID>();
                    object LockObject = new object();
                    Parallel.ForEach(
                        GetCurrentOutfitFolderLinks(CurrentOutfitFolder), o =>
                        {
                            switch (IsBodyPart(o))
                            {
                                case true:
                                    if (
                                        equipItems.AsParallel()
                                            .Where(IsBodyPart)
                                            .Any(
                                                p =>
                                                    ((InventoryWearable) p).WearableType.Equals(
                                                        ((InventoryWearable) ResolveItemLink(o)).WearableType)))
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
                            AddLink(item, CurrentOutfitFolder);
                        }
                    }

                    // And replace the outfit wit hthe new items.
                    lock (Locks.ClientInstanceAppearanceLock)
                    {
                        Client.Appearance.ReplaceOutfit(equipItems, false);
                    }

                    // Update inventory.
                    UpdateInventoryRecursive(CurrentOutfitFolder);

                    // Schedule a rebake.
                    RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                };
        }
    }
}