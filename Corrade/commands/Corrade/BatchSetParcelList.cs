///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CorradeConfigurationSharp;
using OpenMetaverse;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                batchsetparcellist =
                    (corradeCommandParameters, result) =>
                    {
                        if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Land))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        var action = Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(KeyValue.Get(wasOutput(
                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message)));
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
                            position = Client.Self.SimPosition;
                        var region =
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                    corradeCommandParameters.Message));
                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                        var simulator = Client.Network.Simulators.AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                    StringComparison.OrdinalIgnoreCase));
                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                        if (simulator == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                        Parcel parcel = null;
                        if (
                            !Services.GetParcelAtPosition(Client, simulator, position,
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                ref parcel))
                            throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
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
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACCESS_LIST_TYPE);
                        var initialGroup = Client.Self.ActiveGroup;
                        var accessType = (AccessList) accessField.GetValue(null);
                        if (!simulator.IsEstateManager && !parcel.OwnerID.Equals(Client.Self.AgentID))
                            switch (accessType)
                            {
                                case AccessList.Access:
                                    if (
                                        !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                            parcel.GroupID,
                                            GroupPowers.LandManageAllowed, corradeConfiguration.ServicesTimeout,
                                            corradeConfiguration.DataTimeout,
                                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                                    break;

                                case AccessList.Ban:
                                    if (
                                        !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                            parcel.GroupID,
                                            GroupPowers.LandManageBanned,
                                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                                    break;

                                case AccessList.Both:
                                    if (
                                        !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                            parcel.GroupID,
                                            GroupPowers.LandManageAllowed, corradeConfiguration.ServicesTimeout,
                                            corradeConfiguration.DataTimeout,
                                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                                    if (
                                        !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                            parcel.GroupID,
                                            GroupPowers.LandManageBanned,
                                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                                    break;
                            }
                        var accessList = new List<ParcelManager.ParcelAccessEntry>();
                        var ParcelAccessListAlarm = new DecayingAlarm(corradeConfiguration.DataDecayType);
                        var LockObject = new object();
                        EventHandler<ParcelAccessListReplyEventArgs> ParcelAccessListHandler = (sender, args) =>
                        {
                            if (!args.LocalID.Equals(parcel.LocalID) ||
                                !args.Simulator.RegionID.Equals(simulator.RegionID)) return;

                            ParcelAccessListAlarm.Alarm(corradeConfiguration.DataTimeout);
                            if (args.AccessList != null && args.AccessList.Any())
                                lock (LockObject)
                                {
                                    accessList.AddRange(args.AccessList);
                                }
                        };

                        Locks.ClientInstanceParcelsLock.EnterReadLock();

                        // Activate parcel group.
                        Locks.ClientInstanceGroupsLock.EnterWriteLock();
                        Client.Groups.ActivateGroup(parcel.GroupID);

                        Client.Parcels.ParcelAccessListReply += ParcelAccessListHandler;
                        Client.Parcels.RequestParcelAccessList(simulator, parcel.LocalID, accessType, 0);
                        if (!ParcelAccessListAlarm.Signal.WaitOne((int) corradeConfiguration.ServicesTimeout, true))
                        {
                            Client.Parcels.ParcelAccessListReply -= ParcelAccessListHandler;
                            Locks.ClientInstanceParcelsLock.ExitReadLock();

                            // Activate the initial group.
                            Client.Groups.ActivateGroup(initialGroup);
                            Locks.ClientInstanceGroupsLock.ExitWriteLock();

                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_PARCEL_LIST);
                        }
                        Client.Parcels.ParcelAccessListReply -= ParcelAccessListHandler;
                        Locks.ClientInstanceParcelsLock.ExitReadLock();

                        // Activate the initial group.
                        Client.Groups.ActivateGroup(initialGroup);
                        Locks.ClientInstanceGroupsLock.ExitWriteLock();

                        var data = new HashSet<string>();
                        CSV.ToEnumerable(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AVATARS)),
                                        corradeCommandParameters.Message)))
                            .AsParallel()
                            .Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                            {
                                UUID agentUUID;
                                if (!UUID.TryParse(o, out agentUUID))
                                {
                                    var fullName = new List<string>(wasOpenMetaverse.Helpers.GetAvatarNames(o));
                                    if (!fullName.Any() ||
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

                        // Activate parcel group.
                        Locks.ClientInstanceGroupsLock.EnterWriteLock();
                        Client.Groups.ActivateGroup(parcel.GroupID);

                        // Update the parcel list.
                        if (!Services.UpdateParcelAccessList(Client, simulator, parcel.LocalID, accessType, accessList))
                        {
                            // Activate the initial group.
                            Client.Groups.ActivateGroup(initialGroup);
                            Locks.ClientInstanceGroupsLock.ExitWriteLock();

                            throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_UPDATE_PARCEL_LIST);
                        }

                        // Activate the initial group.
                        Client.Groups.ActivateGroup(initialGroup);
                        Locks.ClientInstanceGroupsLock.ExitWriteLock();

                        // Return any avatars that could not have been processed.
                        if (data.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(data));
                    };
        }
    }
}