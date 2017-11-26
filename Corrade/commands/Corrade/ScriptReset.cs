///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CorradeConfigurationSharp;
using OpenMetaverse;
using OpenMetaverse.Packets;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> scriptreset
                =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    bool all;
                    if (!bool.TryParse(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ALL)),
                            corradeCommandParameters.Message)), out all))
                        all = false;
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                            out range))
                        range = corradeConfiguration.Range;
                    Primitive primitive = null;
                    var item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                    UUID itemUUID;
                    switch (UUID.TryParse(item, out itemUUID))
                    {
                        case true:
                            if (
                                !Services.FindPrimitive(Client,
                                    itemUUID,
                                    range,
                                    ref primitive,
                                    corradeConfiguration.DataTimeout))
                                throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                            break;

                        default:
                            if (
                                !Services.FindPrimitive(Client,
                                    item,
                                    range,
                                    ref primitive,
                                    corradeConfiguration.DataTimeout))
                                throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                            break;
                    }
                    var simulator = Client.Network.Simulators.FirstOrDefault(
                        o => o.Handle.Equals(primitive.RegionHandle));
                    if (simulator == null || simulator.Equals(default(Simulator)))
                        throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                    var inventory = new List<InventoryBase>();
                    Locks.ClientInstanceInventoryLock.EnterReadLock();
                    inventory.AddRange(
                        Client.Inventory.GetTaskInventory(primitive.ID, primitive.LocalID,
                            (int) corradeConfiguration.ServicesTimeout));
                    Locks.ClientInstanceInventoryLock.ExitReadLock();
                    switch (all)
                    {
                        case true:
                            inventory.AsParallel().WithDegreeOfParallelism(6).ForAll(o =>
                            {
                                Locks.ClientInstanceNetworkLock.EnterReadLock();
                                Client.Network.SendPacket(new ScriptResetPacket
                                {
                                    Type = PacketType.ScriptReset,
                                    AgentData = new ScriptResetPacket.AgentDataBlock
                                    {
                                        AgentID = Client.Self.AgentID,
                                        SessionID = Client.Self.SessionID
                                    },
                                    Script = new ScriptResetPacket.ScriptBlock
                                    {
                                        ItemID = o.UUID,
                                        ObjectID = primitive.ID
                                    }
                                }, simulator);
                                Locks.ClientInstanceNetworkLock.ExitReadLock();
                            });
                            break;

                        default:
                            var entity = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                    corradeCommandParameters.Message));
                            UUID entityUUID;
                            if (!UUID.TryParse(entity, out entityUUID))
                            {
                                if (string.IsNullOrEmpty(entity))
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                                entityUUID = UUID.Zero;
                            }
                            var inventoryItem = !entityUUID.Equals(UUID.Zero)
                                ? inventory.AsParallel().FirstOrDefault(o => o.UUID.Equals(entityUUID)) as InventoryItem
                                : inventory.AsParallel().FirstOrDefault(o => o.Name.Equals(entity)) as InventoryItem;
                            if (inventoryItem == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                            switch (inventoryItem.AssetType)
                            {
                                case AssetType.LSLBytecode:
                                case AssetType.LSLText:
                                    break;

                                default:
                                    throw new Command.ScriptException(Enumerations.ScriptError.ITEM_IS_NOT_A_SCRIPT);
                            }
                            Locks.ClientInstanceNetworkLock.EnterReadLock();
                            Client.Network.SendPacket(new ScriptResetPacket
                            {
                                Type = PacketType.ScriptReset,
                                AgentData = new ScriptResetPacket.AgentDataBlock
                                {
                                    AgentID = Client.Self.AgentID,
                                    SessionID = Client.Self.SessionID
                                },
                                Script = new ScriptResetPacket.ScriptBlock
                                {
                                    ItemID = inventoryItem.UUID,
                                    ObjectID = primitive.ID
                                }
                            }, simulator);
                            Locks.ClientInstanceNetworkLock.ExitReadLock();
                            break;
                    }
                };
        }
    }
}