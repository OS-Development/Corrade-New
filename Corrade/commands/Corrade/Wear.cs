///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> wear =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string wearables =
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.WEARABLES)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(wearables))
                    {
                        throw new ScriptException(ScriptError.EMPTY_WEARABLES);
                    }
                    bool replace;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.REPLACE)),
                                    corradeCommandParameters.Message)),
                            out replace))
                    {
                        replace = true;
                    }
                    CSV.ToEnumerable(wearables)
                        .ToArray()
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o))
                        .Select(
                            o =>
                            {
                                InventoryItem inventoryItem;
                                UUID itemUUID;
                                if (UUID.TryParse(o, out itemUUID))
                                {
                                    InventoryBase inventoryBaseItem =
                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                            itemUUID
                                            ).FirstOrDefault();
                                    if (inventoryBaseItem == null)
                                        return null;
                                    inventoryItem = inventoryBaseItem as InventoryItem;
                                }
                                else
                                {
                                    // attempt regex and then fall back to string
                                    InventoryBase inventoryBaseItem = null;
                                    try
                                    {
                                        inventoryBaseItem =
                                            Inventory.FindInventory<InventoryBase>(Client,
                                                Client.Inventory.Store.RootNode,
                                                new Regex(o, RegexOptions.Compiled | RegexOptions.IgnoreCase))
                                                .FirstOrDefault();
                                    }
                                    catch (Exception)
                                    {
                                        // not a regex so we do not care
                                        inventoryBaseItem =
                                            Inventory.FindInventory<InventoryBase>(Client,
                                                Client.Inventory.Store.RootNode, o)
                                                .FirstOrDefault();
                                    }
                                    if (inventoryBaseItem == null)
                                        return null;
                                    inventoryItem = inventoryBaseItem as InventoryItem;
                                }
                                if (inventoryItem == null)
                                    return null;

                                if (inventoryItem is InventoryWearable)
                                    return null;
                                return inventoryItem;
                            })
                        .Where(o => o != null)
                        .ForAll(
                            o =>
                            {
                                Inventory.Wear(Client, CurrentOutfitFolder, o, replace,
                                    corradeConfiguration.ServicesTimeout);
                                CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                    () => SendNotification(
                                        Configuration.Notifications.OutfitChanged,
                                        new OutfitEventArgs
                                        {
                                            Action = Action.WEAR,
                                            Name = o.Name,
                                            Description = o.Description,
                                            Item = o.UUID,
                                            Asset = o.AssetUUID,
                                            Entity = o.AssetType,
                                            Creator = o.CreatorID,
                                            Permissions =
                                                Inventory.wasPermissionsToString(
                                                    o.Permissions),
                                            Inventory = o.InventoryType,
                                            Replace = replace,
                                            Slot = (o as InventoryWearable).WearableType.ToString()
                                        }),
                                    corradeConfiguration.MaximumNotificationThreads);
                            });
                    RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                };
        }
    }
}