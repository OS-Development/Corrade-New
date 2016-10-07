///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using Corrade.Constants;
using CorradeConfiguration;
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
                removeitem =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Inventory))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        }
                        var item = wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                                corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(item))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                        }
                        InventoryBase inventoryBase = null;
                        UUID itemUUID;
                        switch (UUID.TryParse(item, out itemUUID))
                        {
                            case true:
                                lock (Locks.ClientInstanceInventoryLock)
                                {
                                    if (Client.Inventory.Store.Contains(itemUUID))
                                    {
                                        inventoryBase = Client.Inventory.Store[itemUUID];
                                    }
                                }
                                break;
                            default:
                                inventoryBase = Inventory.FindInventory<InventoryBase>(Client, item,
                                    CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                    corradeConfiguration.ServicesTimeout);
                                break;
                        }
                        if (inventoryBase == null)
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                        }
                        switch (inventoryBase is InventoryFolder)
                        {
                            case true:
                                lock (Locks.ClientInstanceInventoryLock)
                                {
                                    Client.Inventory.RemoveFolder(inventoryBase.UUID);
                                }
                                break;
                            default:
                                lock (Locks.ClientInstanceInventoryLock)
                                {
                                    Client.Inventory.RemoveItem(inventoryBase.UUID);
                                }
                                break;
                        }
                    };
        }
    }
}