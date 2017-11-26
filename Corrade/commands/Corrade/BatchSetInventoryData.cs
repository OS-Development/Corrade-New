///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Constants;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                batchsetinventorydata
                    =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Inventory))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        var LockObject = new object();
                        var data = new HashSet<string>();
                        CSV.ToEnumerable(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                                    corradeCommandParameters.Message))).AsParallel()
                            .Where(o => !string.IsNullOrEmpty(o))
                            .ForAll(item =>
                            {
                                InventoryBase inventoryBase = null;
                                UUID itemUUID;
                                switch (UUID.TryParse(item, out itemUUID))
                                {
                                    case true:
                                        Locks.ClientInstanceInventoryLock.EnterReadLock();
                                        if (Client.Inventory.Store.Contains(itemUUID))
                                            inventoryBase = Client.Inventory.Store[itemUUID];
                                        Locks.ClientInstanceInventoryLock.ExitReadLock();
                                        break;

                                    default:
                                        inventoryBase = Inventory.FindInventory<InventoryBase>(Client, item,
                                            CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                            corradeConfiguration.ServicesTimeout);
                                        break;
                                }
                                // Could not find inventory data so add it to the reject list.
                                if (inventoryBase == null)
                                {
                                    lock (LockObject)
                                    {
                                        if (!data.Contains(item))
                                            data.Add(item);
                                    }
                                    return;
                                }
                                // Item is not an inventory item so add it to the reject list.
                                var inventoryitem = inventoryBase as InventoryItem;
                                if (inventoryitem == null)
                                {
                                    lock (LockObject)
                                    {
                                        if (!data.Contains(item))
                                            data.Add(item);
                                    }
                                    return;
                                }
                                inventoryitem =
                                    inventoryitem.wasCSVToStructure(
                                        wasInput(KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                            corradeCommandParameters.Message)), wasInput);
                                Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                Client.Inventory.RequestUpdateItem(inventoryitem);
                                Locks.ClientInstanceInventoryLock.ExitWriteLock();
                            });
                        if (data.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(data));
                    };
        }
    }
}