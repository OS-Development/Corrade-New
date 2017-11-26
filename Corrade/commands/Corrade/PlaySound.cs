///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> playsound =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                        position = Client.Self.SimPosition;
                    var region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                corradeCommandParameters.Message));
                    Simulator simulator;
                    switch (!string.IsNullOrEmpty(region))
                    {
                        case true:
                            Locks.ClientInstanceNetworkLock.EnterReadLock();
                            simulator =
                                Client.Network.Simulators.AsParallel().FirstOrDefault(
                                    o =>
                                        o.Name.Equals(
                                            string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                            StringComparison.OrdinalIgnoreCase));
                            Locks.ClientInstanceNetworkLock.ExitReadLock();
                            if (simulator == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                            break;

                        default:
                            simulator = Client.Network.CurrentSim;
                            break;
                    }
                    float gain;
                    if (!float.TryParse(
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.GAIN)),
                            corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                        out gain))
                        gain = 1;
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
                    Locks.ClientInstanceSoundLock.EnterWriteLock();
                    Client.Sound.SendSoundTrigger(itemUUID, simulator, position, gain);
                    Locks.ClientInstanceSoundLock.ExitWriteLock();
                };
        }
    }
}