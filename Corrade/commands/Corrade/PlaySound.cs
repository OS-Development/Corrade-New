///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> playsound =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION)),
                                    message)),
                            out position))
                    {
                        position = Client.Self.SimPosition;
                    }
                    float gain;
                    if (!float.TryParse(
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.GAIN)),
                            message)),
                        out gain))
                    {
                        gain = 1;
                    }
                    object item =
                        StringOrUUID(
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)),
                                message)));
                    InventoryItem inventoryItem;
                    switch (item != null)
                    {
                        case true:
                            InventoryBase inventoryBaseItem =
                                FindInventory<InventoryBase>(Client.Inventory.Store.RootNode, item
                                    ).FirstOrDefault();
                            if (inventoryBaseItem == null)
                            {
                                throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                            }
                            inventoryItem = inventoryBaseItem as InventoryItem;
                            if (inventoryItem == null)
                            {
                                throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
                    }
                    Client.Sound.SendSoundTrigger(inventoryItem.UUID, position, gain);
                };
        }
    }
}