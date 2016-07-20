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
using wasOpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;

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
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
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
                    var region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.REGION)),
                                corradeCommandParameters.Message));
                    Simulator simulator;
                    switch (!string.IsNullOrEmpty(region))
                    {
                        case true:
                            lock (Locks.ClientInstanceNetworkLock)
                            {
                                simulator =
                                    Client.Network.Simulators.AsParallel().FirstOrDefault(
                                        o =>
                                            o.Name.Equals(
                                                string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                                StringComparison.OrdinalIgnoreCase));
                            }
                            if (simulator == null)
                            {
                                throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                            }
                            break;
                        default:
                            simulator = Client.Network.CurrentSim;
                            break;
                    }
                    float gain;
                    if (!float.TryParse(
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.GAIN)),
                            corradeCommandParameters.Message)),
                        out gain))
                    {
                        gain = 1;
                    }
                    var item = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                    {
                        throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
                    }
                    UUID itemUUID;
                    // If the asset is of an asset type that can only be retrieved locally or the item is a string
                    // then attempt to resolve the item to an inventory item or else the item cannot be found.
                    if (!UUID.TryParse(item, out itemUUID))
                    {
                        var inventoryItem =
                            Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode, item,
                                corradeConfiguration.ServicesTimeout)
                                .FirstOrDefault() as InventoryItem;
                        if (inventoryItem == null)
                        {
                            throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                        }
                        itemUUID = inventoryItem.AssetUUID;
                    }
                    lock (Locks.ClientInstanceSoundLock)
                    {
                        Client.Sound.SendSoundTrigger(itemUUID, simulator, position, gain);
                    }
                };
        }
    }
}