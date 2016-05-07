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
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> returnprimitives =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    UUID agentUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT)),
                                corradeCommandParameters.Message)),
                            out agentUUID) && !Resolvers.AgentNameToUUID(Client,
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME)),
                                        corradeCommandParameters.Message)),
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME)),
                                        corradeCommandParameters.Message)),
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType),
                                ref agentUUID))
                    {
                        throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                    }
                    string region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.REGION)),
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
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    }
                    string type =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                            corradeCommandParameters.Message));
                    switch (
                        Reflection.GetEnumValueFromName<Entity>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Entity.PARCEL:
                            Vector3 position;
                            HashSet<Parcel> parcels = new HashSet<Parcel>();
                            switch (Vector3.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION)),
                                        corradeCommandParameters.Message)),
                                out position))
                            {
                                case false:
                                    // Get all sim parcels
                                    ManualResetEvent SimParcelsDownloadedEvent = new ManualResetEvent(false);
                                    EventHandler<SimParcelsDownloadedEventArgs> SimParcelsDownloadedEventHandler =
                                        (sender, args) => SimParcelsDownloadedEvent.Set();
                                    lock (Locks.ClientInstanceParcelsLock)
                                    {
                                        Client.Parcels.SimParcelsDownloaded += SimParcelsDownloadedEventHandler;
                                        Client.Parcels.RequestAllSimParcels(simulator);
                                        if (simulator.IsParcelMapFull())
                                        {
                                            SimParcelsDownloadedEvent.Set();
                                        }
                                        if (
                                            !SimParcelsDownloadedEvent.WaitOne(
                                                (int) corradeConfiguration.ServicesTimeout, false))
                                        {
                                            Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_PARCELS);
                                        }
                                        Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                                    }
                                    simulator.Parcels.ForEach(o => parcels.Add(o));
                                    break;
                                case true:
                                    Parcel parcel = null;
                                    if (
                                        !Services.GetParcelAtPosition(Client, simulator, position,
                                            corradeConfiguration.ServicesTimeout, ref parcel))
                                    {
                                        throw new ScriptException(ScriptError.COULD_NOT_FIND_PARCEL);
                                    }
                                    parcels.Add(parcel);
                                    break;
                            }
                            FieldInfo objectReturnTypeField = typeof (ObjectReturnType).GetFields(
                                BindingFlags.Public |
                                BindingFlags.Static)
                                .AsParallel().FirstOrDefault(
                                    o =>
                                        o.Name.Equals(type
                                            .ToLowerInvariant(),
                                            StringComparison.Ordinal));
                            ObjectReturnType returnType = objectReturnTypeField != null
                                ? (ObjectReturnType)
                                    objectReturnTypeField
                                        .GetValue(null)
                                : ObjectReturnType.Other;
                            if (!simulator.IsEstateManager)
                            {
                                bool gotPermission = true;
                                Parallel.ForEach(parcels.ToArray()
                                    .AsParallel()
                                    .Where(o => !o.OwnerID.Equals(Client.Self.AgentID)), (o, s) =>
                                    {
                                        if (!o.IsGroupOwned ||
                                            !o.GroupID.Equals(corradeCommandParameters.Group.UUID))
                                        {
                                            gotPermission = false;
                                            s.Break();
                                        }
                                        GroupPowers power = new GroupPowers();
                                        switch (returnType)
                                        {
                                            case ObjectReturnType.Other:
                                                power = GroupPowers.ReturnNonGroup;
                                                break;
                                            case ObjectReturnType.Group:
                                                power = GroupPowers.ReturnGroupSet;
                                                break;
                                            case ObjectReturnType.Owner:
                                                power = GroupPowers.ReturnGroupOwned;
                                                break;
                                        }
                                        if (
                                            !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                                corradeCommandParameters.Group.UUID,
                                                power,
                                                corradeConfiguration.ServicesTimeout,
                                                corradeConfiguration.DataTimeout,
                                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                                        {
                                            gotPermission = false;
                                            s.Break();
                                        }
                                    });
                                if (!gotPermission)
                                    throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                            }
                            lock (Locks.ClientInstanceParcelsLock)
                            {
                                Parallel.ForEach(parcels,
                                    o =>
                                        Client.Parcels.ReturnObjects(simulator, o.LocalID,
                                            returnType
                                            , new List<UUID> {agentUUID}));
                            }
                            break;
                        case Entity.ESTATE:
                            if (!simulator.IsEstateManager)
                            {
                                throw new ScriptException(ScriptError.NO_LAND_RIGHTS);
                            }
                            bool allEstates;
                            if (
                                !bool.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ALL)),
                                            corradeCommandParameters.Message)),
                                    out allEstates))
                            {
                                allEstates = false;
                            }
                            FieldInfo estateReturnFlagsField = typeof (EstateTools.EstateReturnFlags).GetFields(
                                BindingFlags.Public | BindingFlags.Static)
                                .AsParallel().FirstOrDefault(
                                    o =>
                                        o.Name.Equals(type,
                                            StringComparison.Ordinal));
                            lock (Locks.ClientInstanceEstateLock)
                            {
                                Client.Estate.SimWideReturn(agentUUID, estateReturnFlagsField != null
                                    ? (EstateTools.EstateReturnFlags)
                                        estateReturnFlagsField
                                            .GetValue(null)
                                    : EstateTools.EstateReturnFlags.ReturnScriptedAndOnOthers, allEstates);
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                    }
                };
        }
    }
}