///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using CorradeConfigurationSharp;
using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Timers;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> batchsetparcellist =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int)Configuration.Permissions.Land))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var action = Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(KeyValue.Get(wasOutput(
                            Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)), corradeCommandParameters.Message)));
                    switch (action)
                    {
                        case Enumerations.Action.ADD:
                        case Enumerations.Action.REMOVE:
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
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
                    var accessType = (AccessList)accessField.GetValue(null);
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

                                case AccessList.Both:
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
                    List<ParcelManager.ParcelAccessEntry> accessList = new List<ParcelManager.ParcelAccessEntry>();
                    wasSharp.Timers.DecayingAlarm ParcelAccessListAlarm = new wasSharp.Timers.DecayingAlarm(corradeConfiguration.DataDecayType);
                    EventHandler<ParcelAccessListReplyEventArgs> ParcelAccessListHandler = (sender, args) =>
                    {
                        if (!args.LocalID.Equals(parcel.LocalID)) return;

                        ParcelAccessListAlarm.Alarm(corradeConfiguration.DataTimeout);
                        if (args.AccessList != null && args.AccessList.Any())
                            accessList.AddRange(args.AccessList);
                    };
                    lock (Locks.ClientInstanceParcelsLock)
                    {
                        Client.Parcels.ParcelAccessListReply += ParcelAccessListHandler;
                        Client.Parcels.RequestParcelAccessList(simulator, parcel.LocalID, accessType, random);
                        if (!ParcelAccessListAlarm.Signal.WaitOne((int)corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Parcels.ParcelAccessListReply -= ParcelAccessListHandler;
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_PARCEL_LIST);
                        }
                        Client.Parcels.ParcelAccessListReply -= ParcelAccessListHandler;
                    }

                    var data = new HashSet<string>();
                    var LockObject = new object();
                    CSV.ToEnumerable(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AVATARS)),
                                corradeCommandParameters.Message)))
                        .ToArray()
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                        {
                            UUID agentUUID;
                            if (!UUID.TryParse(o, out agentUUID))
                            {
                                var fullName = new List<string>(wasOpenMetaverse.Helpers.GetAvatarNames(o));
                                if (fullName == null ||
                                    !Resolvers.AgentNameToUUID(Client, fullName.First(), fullName.Last(),
                                        corradeConfiguration.ServicesTimeout,
                                        corradeConfiguration.DataTimeout,
                                        new DecayingAlarm(corradeConfiguration.DataDecayType), ref agentUUID))
                                {
                                    // Add all the unrecognized agents to the returned list.
                                    lock (LockObject)
                                    {
                                        if (!data.Contains(o))
                                            data.Add(o);
                                    }
                                    return;
                                }
                            }

                            switch (action)
                            {
                                case Enumerations.Action.ADD:
                                    if (accessList.AsParallel().Any(p => p.AgentID.Equals(agentUUID)))
                                    {
                                        lock (LockObject)
                                        {
                                            if (!data.Contains(o))
                                                data.Add(o);
                                        }
                                        return;
                                    }
                                    accessList.Add(new ParcelManager.ParcelAccessEntry
                                    {
                                        AgentID = agentUUID,
                                        Flags = accessType,
                                        Time = DateTime.UtcNow
                                    });
                                    break;

                                case Enumerations.Action.REMOVE:
                                    if (!accessList.AsParallel().Any(p => p.AgentID.Equals(agentUUID)))
                                    {
                                        lock (LockObject)
                                        {
                                            if (!data.Contains(o))
                                                data.Add(o);
                                        }
                                        return;
                                    }
                                    accessList.RemoveAll(p => p.AgentID.Equals(agentUUID));
                                    break;
                            }
                        });

                    // Update the parcel list.
                    if (!Services.UpdateParcelAccessList(Client, simulator, parcel.LocalID, accessType, accessList))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_UPDATE_PARCEL_LIST);
                    }

                    // Return any avatars that could not have been processed.
                    if (data.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                    }
                };
        }
    }
}
