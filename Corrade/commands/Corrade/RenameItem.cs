///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> renameitem =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var item = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
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
                    if (inventoryBase == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                    var name = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);
                    switch (inventoryBase is InventoryFolder)
                    {
                        case true:
                            Locks.ClientInstanceInventoryLock.EnterWriteLock();
                            Client.Inventory.MoveFolder(inventoryBase.UUID, inventoryBase.ParentUUID, name);
                            Locks.ClientInstanceInventoryLock.ExitWriteLock();
                            break;

                        default:
                            Locks.ClientInstanceInventoryLock.EnterWriteLock();
                            Client.Inventory.MoveItem(inventoryBase.UUID, inventoryBase.ParentUUID, name);
                            Locks.ClientInstanceInventoryLock.ExitWriteLock();
                            break;
                    }
                };
        }
    }
}