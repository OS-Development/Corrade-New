///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> changeappearance =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Grooming))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string folder =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.FOLDER)),
                            message));
                    if (string.IsNullOrEmpty(folder))
                    {
                        throw new ScriptException(ScriptError.NO_FOLDER_SPECIFIED);
                    }
                    // Check for items that can be worn.
                    List<InventoryBase> items =
                        GetInventoryFolderContents<InventoryBase>(Client.Inventory.Store.RootNode, folder)
                            .AsParallel().Where(CanBeWorn)
                            .ToList();
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
                            if (item is InventoryAttachment || item is InventoryObject)
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
                            Wear(item, false);
                            return;
                        }
                        if (item is InventoryAttachment || item is InventoryObject)
                        {
                            Attach(item, AttachmentPoint.Default, false);
                        }
                    });
                    // And rebake.
                    RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                };
        }
    }
}