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
            public static Action<Group, string, Dictionary<string, string>> rez = (commandGroup, message, result) =>
            {
                if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Inventory))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                InventoryBase inventoryBaseItem =
                    FindInventory<InventoryBase>(Client.Inventory.Store.RootNode,
                        StringOrUUID(wasInput(wasKeyValueGet(
                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)), message)))
                        ).FirstOrDefault();
                if (inventoryBaseItem == null)
                {
                    throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                }
                Vector3 position;
                if (
                    !Vector3.TryParse(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION)),
                                message)),
                        out position))
                {
                    throw new ScriptException(ScriptError.INVALID_POSITION);
                }
                if (IsSecondLife() &&
                    position.Z > LINDEN_CONSTANTS.PRIMITIVES.MAXIMUM_REZ_HEIGHT)
                {
                    throw new Exception(
                        wasGetDescriptionFromEnumValue(
                            ScriptError.POSITION_WOULD_EXCEED_MAXIMUM_REZ_ALTITUDE));
                }
                Quaternion rotation;
                if (
                    !Quaternion.TryParse(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ROTATION)),
                                message)),
                        out rotation))
                {
                    rotation = Quaternion.CreateFromEulers(0, 0, 0);
                }
                string region =
                    wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REGION)),
                        message));
                Simulator simulator =
                    Client.Network.Simulators.FirstOrDefault(
                        o =>
                            o.Name.Equals(
                                string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                StringComparison.OrdinalIgnoreCase));
                if (simulator == null)
                {
                    throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                }
                Parcel parcel = null;
                if (!GetParcelAtPosition(simulator, position, ref parcel))
                {
                    throw new ScriptException(ScriptError.COULD_NOT_FIND_PARCEL);
                }
                if (((uint) parcel.Flags & (uint) ParcelFlags.CreateObjects).Equals(0))
                {
                    if (!simulator.IsEstateManager)
                    {
                        if (!parcel.OwnerID.Equals(Client.Self.AgentID))
                        {
                            if (!parcel.IsGroupOwned && !parcel.GroupID.Equals(commandGroup.UUID))
                            {
                                throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                            }
                            if (!HasGroupPowers(Client.Self.AgentID, commandGroup.UUID, GroupPowers.AllowRez,
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                            {
                                throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                            }
                        }
                    }
                }
                Client.Inventory.RequestRezFromInventory(simulator, rotation, position,
                    inventoryBaseItem as InventoryItem,
                    commandGroup.UUID);
            };
        }
    }
}