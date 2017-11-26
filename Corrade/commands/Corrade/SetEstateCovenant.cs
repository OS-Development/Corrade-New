///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                setestatecovenant =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Land))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                        if (!Client.Network.CurrentSim.IsEstateManager)
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_ESTATE_POWERS_FOR_COMMAND);

                        var item = wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                                corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(item))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);

                        UUID itemUUID;
                        // If the asset is of an asset type that can only be retrieved locally or the item is a string
                        // then attempt to resolve the item to an inventory item or else the item cannot be found.
                        if (!UUID.TryParse(item, out itemUUID))
                        {
                            var inventoryItem = Inventory.FindInventory<InventoryItem>(Client, item,
                                CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                corradeConfiguration.ServicesTimeout);
                            if (inventoryItem == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                            itemUUID = inventoryItem.AssetUUID;
                        }

                        Locks.ClientInstanceEstateLock.EnterWriteLock();
                        Client.Estate.EstateOwnerMessage("estatechangecovenantid", itemUUID.ToString());
                        Locks.ClientInstanceEstateLock.ExitWriteLock();
                    };
        }
    }
}