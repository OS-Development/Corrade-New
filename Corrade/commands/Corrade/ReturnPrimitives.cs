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
using Parallel = System.Threading.Tasks.Parallel;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> returnprimitives
                =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int) Configuration.Permissions.Land))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    UUID agentUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                corradeCommandParameters.Message)),
                            out agentUUID) && !Resolvers.AgentNameToUUID(Client,
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME)),
                                    corradeCommandParameters.Message)),
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                    corradeCommandParameters.Message)),
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType),
                            ref agentUUID))
                        throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
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
                    var type =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                            corradeCommandParameters.Message));
                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Entity>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                    corradeCommandParameters.Message))))
                    {
                        case Enumerations.Entity.PARCEL:
                            Vector3 position;
                            var parcels = new HashSet<Parcel>();
                            switch (Vector3.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                        corradeCommandParameters.Message)),
                                out position))
                            {
                                case false:
                                    // Get all sim parcels
                                    var SimParcelsDownloadedEvent = new ManualResetEventSlim(false);
                                    EventHandler<SimParcelsDownloadedEventArgs> SimParcelsDownloadedEventHandler =
                                        (sender, args) => SimParcelsDownloadedEvent.Set();
                                    Locks.ClientInstanceParcelsLock.EnterReadLock();
                                    Client.Parcels.SimParcelsDownloaded += SimParcelsDownloadedEventHandler;
                                    Client.Parcels.RequestAllSimParcels(simulator, true,
                                        (int) corradeConfiguration.DataTimeout);
                                    if (simulator.IsParcelMapFull())
                                        SimParcelsDownloadedEvent.Set();
                                    if (
                                        !SimParcelsDownloadedEvent.Wait(
                                            (int) corradeConfiguration.ServicesTimeout))
                                    {
                                        Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                                        Locks.ClientInstanceParcelsLock.ExitReadLock();
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.TIMEOUT_GETTING_PARCELS);
                                    }
                                    Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                                    Locks.ClientInstanceParcelsLock.ExitReadLock();
                                    simulator.Parcels.ForEach(o => parcels.Add(o));
                                    break;

                                case true:
                                    Parcel parcel = null;
                                    if (
                                        !Services.GetParcelAtPosition(Client, simulator, position,
                                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                            ref parcel))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                                    parcels.Add(parcel);
                                    break;
                            }
                            var objectReturnTypeField = typeof(ObjectReturnType).GetFields(
                                    BindingFlags.Public |
                                    BindingFlags.Static)
                                .AsParallel().FirstOrDefault(
                                    o => string.Equals(o.Name, type, StringComparison.OrdinalIgnoreCase));
                            var returnType = objectReturnTypeField != null
                                ? (ObjectReturnType)
                                objectReturnTypeField
                                    .GetValue(null)
                                : ObjectReturnType.Other;
                            if (!simulator.IsEstateManager)
                            {
                                var gotPermission = true;
                                Parallel.ForEach(parcels
                                    .AsParallel()
                                    .Where(o => !o.OwnerID.Equals(Client.Self.AgentID)), (o, s) =>
                                {
                                    if (!o.IsGroupOwned ||
                                        !o.GroupID.Equals(corradeCommandParameters.Group.UUID))
                                    {
                                        gotPermission = false;
                                        s.Break();
                                    }
                                    var power = new GroupPowers();
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
                                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                                    {
                                        gotPermission = false;
                                        s.Break();
                                    }
                                });
                                if (!gotPermission)
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                            }
                            Locks.ClientInstanceParcelsLock.EnterWriteLock();
                            parcels.AsParallel().ForAll(
                                o =>
                                    Client.Parcels.ReturnObjects(simulator, o.LocalID,
                                        returnType
                                        , new List<UUID> {agentUUID}));
                            Locks.ClientInstanceParcelsLock.ExitWriteLock();
                            break;

                        case Enumerations.Entity.ESTATE:
                            if (!simulator.IsEstateManager)
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_LAND_RIGHTS);
                            bool allEstates;
                            if (
                                !bool.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ALL)),
                                            corradeCommandParameters.Message)),
                                    out allEstates))
                                allEstates = false;
                            var estateReturnFlagsField = typeof(EstateTools.EstateReturnFlags).GetFields(
                                    BindingFlags.Public | BindingFlags.Static)
                                .AsParallel().FirstOrDefault(
                                    o =>
                                        o.Name.Equals(type,
                                            StringComparison.Ordinal));
                            Locks.ClientInstanceEstateLock.EnterWriteLock();
                            Client.Estate.SimWideReturn(agentUUID, estateReturnFlagsField != null
                                ? (EstateTools.EstateReturnFlags)
                                estateReturnFlagsField
                                    .GetValue(null)
                                : EstateTools.EstateReturnFlags.ReturnScriptedAndOnOthers, allEstates);
                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }
                };
        }
    }
}