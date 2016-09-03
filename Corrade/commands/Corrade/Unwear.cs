///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Events;
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
            public static Action<Command.CorradeCommandParameters, Dictionary<string, string>> unwear =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var wearables =
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.WEARABLES)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(wearables))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.EMPTY_WEARABLES);
                    }
                    CSV.ToEnumerable(
                        wearables).AsParallel().Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                        {
                            InventoryItem inventoryItem;
                            UUID itemUUID;
                            switch (UUID.TryParse(o, out itemUUID))
                            {
                                case true:
                                    inventoryItem =
                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                            itemUUID, corradeConfiguration.ServicesTimeout).FirstOrDefault() as
                                            InventoryItem;
                                    break;
                                default:
                                    inventoryItem =
                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                            o, corradeConfiguration.ServicesTimeout).FirstOrDefault() as InventoryItem;
                                    break;
                            }
                            if (inventoryItem == null)
                                return;

                            if (inventoryItem is InventoryWearable)
                            {
                                CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                    () => SendNotification(
                                        Configuration.Notifications.OutfitChanged,
                                        new OutfitEventArgs
                                        {
                                            Action = Enumerations.Action.UNWEAR,
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