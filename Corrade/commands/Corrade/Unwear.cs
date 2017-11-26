///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Corrade.Constants;
using Corrade.Events;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> unwear =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var wearables =
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.WEARABLES)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(wearables))
                        throw new Command.ScriptException(Enumerations.ScriptError.EMPTY_WEARABLES);

                    var type = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(type))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_TYPE_PROVIDED);

                    var detachType = Reflection.GetEnumValueFromName<Enumerations.Type>(type);
                    var wornWearables = Inventory.GetWearables(
                        Client, CurrentOutfitFolder, corradeConfiguration.ServicesTimeout);
                    CSV.ToEnumerable(
                        wearables).AsParallel().Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                    {
                        InventoryItem inventoryItem = null;
                        switch (detachType)
                        {
                            case Enumerations.Type.SLOT:
                                var wearTypeInfo = typeof(WearableType).GetFields(BindingFlags.Public |
                                                                                  BindingFlags.Static)
                                    .AsParallel().FirstOrDefault(
                                        p =>
                                            string.Equals(o, p.Name,
                                                StringComparison.InvariantCultureIgnoreCase));
                                if (wearTypeInfo == null)
                                    break;
                                inventoryItem = wornWearables
                                    .AsParallel()
                                    .FirstOrDefault(
                                        p => (p as InventoryWearable).WearableType.Equals(
                                            (WearableType) wearTypeInfo.GetValue(null)));
                                break;

                            case Enumerations.Type.PATH:
                                inventoryItem =
                                    Inventory.FindInventory<InventoryItem>(Client, o,
                                        CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                        corradeConfiguration.ServicesTimeout);
                                break;

                            case Enumerations.Type.UUID:
                                UUID itemUUID;
                                if (UUID.TryParse(o, out itemUUID))
                                {
                                    Locks.ClientInstanceInventoryLock.EnterReadLock();
                                    if (Client.Inventory.Store.Contains(itemUUID))
                                        inventoryItem = Client.Inventory.Store[itemUUID] as InventoryItem;
                                    Locks.ClientInstanceInventoryLock.ExitReadLock();
                                }
                                break;
                        }

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