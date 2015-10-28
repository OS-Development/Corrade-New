///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using OpenMetaverse;
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
                            KeyValue.wasKeyValueGet(wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.FOLDER)),
                                corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(folder))
                    {
                        throw new ScriptException(ScriptError.NO_FOLDER_SPECIFIED);
                    }
                    // Check for items that can be worn.
                    List<InventoryBase> items =
                        GetInventoryFolderContents<InventoryBase>(Client.Inventory.Store.RootNode, folder)
                            .AsParallel().Where(CanBeWorn)
                            .ToList();
                    // Check if any items are left over.
                    if (!items.Any())
                    {
                        throw new ScriptException(ScriptError.NO_EQUIPABLE_ITEMS);
                    }
                    // Now remove the current outfit items.
                    Client.Inventory.Store.GetContents(
                        Client.Inventory.FindFolderForType(AssetType.CurrentOutfitFolder)).FindAll(
                            o => CanBeWorn(o) && ((InventoryItem) o).AssetType.Equals(AssetType.Link))
                        .ForEach(p =>
                        {
                            InventoryItem item = ResolveItemLink(p as InventoryItem);
                            if (item is InventoryWearable)
                            {
                                if (!IsBodyPart(item))
                                {
                                    UnWear(item);
                                    return;
                                }
                                if (items.AsParallel().Any(q =>
                                {
                                    InventoryWearable i = q as InventoryWearable;
                                    return i != null &&
                                           ((InventoryWearable) item).WearableType.Equals(i.WearableType);
                                }))
                                    UnWear(item);
                                return;
                            }
                            if (item is InventoryAttachment)
                            {
                                Detach(item);
                                return;
                            }
                            if (item is InventoryObject)
                            {
                                Detach(item);
                            }
                        });

                    // And equip the specified folder.
                    Parallel.ForEach(items, o =>
                    {
                        InventoryItem item = o as InventoryItem;
                        if (item is InventoryWearable)
                        {
                            Wear(item, true);
                            return;
                        }
                        if (item is InventoryAttachment)
                        {
                            Attach(item, AttachmentPoint.Default, true);
                            return;
                        }
                        if (item is InventoryObject)
                        {
                            Attach(item, AttachmentPoint.Default, true);
                        }
                    });

                    // Schedule a rebake.
                    RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                };
        }
    }
}