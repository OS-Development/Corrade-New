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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> unwear =
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
                    CSV.ToEnumerable(
                        wearables).ToArray().AsParallel().Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
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
                                    return;
                                inventoryItem = inventoryBaseItem as InventoryItem;
                            }
                            else
                            {
                                // attempt regex and then fall back to string
                                InventoryBase inventoryBaseItem = null;
                                try
                                {
                                    inventoryBaseItem =
                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                            new Regex(o, RegexOptions.Compiled | RegexOptions.IgnoreCase))
                                            .FirstOrDefault();
                                }
                                catch (Exception)
                                {
                                    // not a regex so we do not care
                                    inventoryBaseItem =
                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                            o)
                                            .FirstOrDefault();
                                }
                                if (inventoryBaseItem == null)
                                    return;
                                inventoryItem = inventoryBaseItem as InventoryItem;
                            }
                            if (inventoryItem == null)
                                return;

                            if (inventoryItem is InventoryWearable)
                            {
                                CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                    () => SendNotification(
                                        Configuration.Notifications.OutfitChanged,
                                        new OutfitEventArgs
                                        {
                                            Action = Action.UNWEAR,
                                            Name = inventoryItem.Name,
                                            Description = inventoryItem.Description,
                                            Item = inventoryItem.UUID,
                                            Asset = inventoryItem.AssetUUID,
                                            Entity = inventoryItem.AssetType,
                                            Creator = inventoryItem.CreatorID,
                                            Permissions =
                                                Inventory.wasPermissionsToString(
                                                    inventoryItem.Permissions),
                                            Inventory = inventoryItem.InventoryType,
                                            Slot = (inventoryItem as InventoryWearable).WearableType.ToString()
                                        }),
                                    corradeConfiguration.MaximumNotificationThreads);
                                Inventory.UnWear(Client, CurrentOutfitFolder, inventoryItem,
                                    corradeConfiguration.ServicesTimeout);
                            }
                        });
                    RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                };
        }
    }
}