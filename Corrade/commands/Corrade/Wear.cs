///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> wear =
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
                    bool replace;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REPLACE)),
                                    corradeCommandParameters.Message)),
                            out replace))
                        replace = true;
                    CSV.ToEnumerable(wearables)
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o))
                        .Select(
                            o =>
                            {
                                InventoryItem inventoryItem = null;
                                UUID itemUUID;
                                switch (UUID.TryParse(o, out itemUUID))
                                {
                                    case true:
                                        Locks.ClientInstanceInventoryLock.EnterReadLock();
                                        if (Client.Inventory.Store.Contains(itemUUID))
                                            inventoryItem = Client.Inventory.Store[itemUUID] as InventoryItem;
                                        Locks.ClientInstanceInventoryLock.ExitReadLock();
                                        break;

                                    default:
                                        inventoryItem =
                                            Inventory.FindInventory<InventoryItem>(Client, o,
                                                CORRADE_CONSTANTS.PATH_SEPARATOR,
                                                CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                                corradeConfiguration.ServicesTimeout);
                                        break;
                                }

                                return inventoryItem;
                            })
                        .Where(Inventory.CanBeWorn)
                        .ForAll(
                            o =>
                            {
                                Inventory.Wear(Client, CurrentOutfitFolder, o, replace,
                                    corradeConfiguration.ServicesTimeout);
                                CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                    () => SendNotification(
                                        Configuration.Notifications.OutfitChanged,
                                        new OutfitEventArgs
                                        {
                                            Action = Enumerations.Action.WEAR,
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