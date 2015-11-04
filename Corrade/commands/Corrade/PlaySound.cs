///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> playsound =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                    {
                        position = Client.Self.SimPosition;
                    }
                    float gain;
                    if (!float.TryParse(
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.GAIN)),
                            corradeCommandParameters.Message)),
                        out gain))
                    {
                        gain = 1;
                    }
                    object item =
                        StringOrUUID(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                                    corradeCommandParameters.Message)));
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