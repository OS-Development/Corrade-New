///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Timers;
using Reflection = wasSharp.Reflection;
using OpenMetaverse.Packets;
using static OpenMetaverse.Packets.ParcelAccessListReplyPacket;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            /// <remarks>
            ///     This command is disabled because libopenmetaverse does not support managing the parcel lists.
            /// </remarks>
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> setparcellist =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Land))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                    {
                        position = Client.Self.SimPosition;
                    }
                    var region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                corradeCommandParameters.Message));
                    Simulator simulator;
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
                        throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                    }
                    Parcel parcel = null;
                    if (
                        !Services.GetParcelAtPosition(Client, simulator, position, corradeConfiguration.ServicesTimeout,
                            ref parcel))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                    }
                    UUID targetUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                    corradeCommandParameters.Message)), out targetUUID) &&
                        !Resolvers.AgentNameToUUID(Client,
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(
                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME)),
                                    corradeCommandParameters.Message)),
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                    corradeCommandParameters.Message)),
                            corradeConfiguration.ServicesTimeout,
                            corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType),
                            ref targetUUID))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                    }
                    var accessField = typeof(AccessList).GetFields(
                        BindingFlags.Public | BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                            corradeCommandParameters.Message)),
                                    StringComparison.Ordinal));
                    if (accessField == null)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACCESS_LIST_TYPE);
                    }
                    var accessType = (AccessList) accessField.GetValue(null);
                    if (!simulator.IsEstateManager)
                    {
                        if (!parcel.OwnerID.Equals(Client.Self.AgentID))
                        {
                            if (!parcel.IsGroupOwned && !parcel.GroupID.Equals(corradeCommandParameters.Group.UUID))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                            }
                            switch (accessType)
                            {
                                case AccessList.Access:
                                    if (
                                        !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                            corradeCommandParameters.Group.UUID,
                                            GroupPowers.LandManageAllowed, corradeConfiguration.ServicesTimeout,
                                            corradeConfiguration.DataTimeout,
                                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                                    {
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                                    }
                                    break;
                                case AccessList.Ban:
                                    if (
                                        !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                            corradeCommandParameters.Group.UUID,
                                            GroupPowers.LandManageBanned,
                                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                                    {
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                                    }
                                    break;
                            }
                        }
                    }
                    var random = new Random().Next();
                    var ParcelAccessListEvent = new ManualResetEvent(false);
                    List<ParcelManager.ParcelAccessEntry> accessList = null;
                    EventHandler<ParcelAccessListReplyEventArgs> ParcelAccessListHandler = (sender, args) =>
                    {
                        if (!args.SequenceID.Equals(random)) return;
                        accessList = args.AccessList;
                        ParcelAccessListEvent.Set();
                    };
                    lock (Locks.ClientInstanceParcelsLock)
                    {
                        Client.Parcels.ParcelAccessListReply += ParcelAccessListHandler;
                        Client.Parcels.RequestParcelAccessList(simulator, parcel.LocalID, accessType, random);
                        if (!ParcelAccessListEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Parcels.ParcelAccessListReply -= ParcelAccessListHandler;
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_PARCELS);
                        }
                        Client.Parcels.ParcelAccessListReply -= ParcelAccessListHandler;
                    }
                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))
                            ))
                    {
                        case Enumerations.Action.ADD:
                            accessList.Add(new ParcelManager.ParcelAccessEntry
                            {
                                AgentID = targetUUID,
                                Flags = accessType,
                                Time = DateTime.UtcNow
                            });
                            break;
                        case Enumerations.Action.REMOVE:
                            accessList.RemoveAll(o => o.AgentID.Equals(targetUUID));
                            break;
                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                    /*switch (accessType)
                    {
                        case AccessList.Ban:
                            parcel.AccessBlackList = accessList;
                            break;
                        case AccessList.Access:
                            parcel.AccessWhiteList = accessList;
                            break;
                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACCESS_LIST_TYPE);
                    }*/
                    /*accessList.AsParallel().ForAll(o =>
                    {
                        var e = new ParcelManager.ParcelAccessEntry()
                        {
                            AgentID = o.AgentID,
                            Flags = accessType,
                            Time = (int)Utils.DateTimeToUnixTime(DateTime.UtcNow)
                        };

                    });*/
                    var transactionUUID = UUID.Random();
                    var parcelAccessListBlock = accessList.AsParallel().Select(o => new ParcelAccessListUpdatePacket.ListBlock
                    {
                        ID = o.AgentID,
                        Flags = (uint)accessType,
                        Time = (int)Utils.DateTimeToUnixTime(DateTime.UtcNow)
                    });
                    ParcelAccessListUpdatePacket p = new ParcelAccessListUpdatePacket
                    {
                        List = parcelAccessListBlock.ToArray(),
                        AgentData = new ParcelAccessListUpdatePacket.AgentDataBlock
                        {
                            AgentID = Client.Self.AgentID,
                            SessionID = Client.Self.SessionID
                        },
                        Data = new ParcelAccessListUpdatePacket.DataBlock
                        {
                            Flags = (uint)accessType,
                            LocalID = parcel.LocalID,
                            TransactionID = transactionUUID,
                            SequenceID = 1,
                            Sections = (int)Math.Ceiling(accessList.Count / 48f)

                        },
                        Type = PacketType.ParcelAccessListUpdate,

                    };

                    Client.Network.SendPacket(p, simulator);
                    


                    /*ParcelAccessListUpdatePacket p = new ParcelAccessListUpdatePacket();
                    p.Type = PacketType.ParcelAccessListUpdate;
                    ParcelManager.ParcelAccessEntry entry = new ParcelManager.ParcelAccessEntry();
                    entry.AgentID = Client.Self.AgentID;
                    entry.Flags = AccessList.Access;
                    entry.Time = DateTime.UtcNow;
                    var accessListEntries = new List<ParcelManager.ParcelAccessEntry>();
                    accessListEntries.Add(entry);
                    p.List = new ListBlock[]
                    {
                        new ParcelAccessListUpdatePacket.ListBlock
                        {
                            Flags = (uint)entry.Flags,
                            Time = (int)Utils.DateTimeToUnixTime(entry.Time),
                            ID = entry.AgentID
                        }
                    };*/

                    //parcel.Update(simulator, true);
                };
        }
    }
}