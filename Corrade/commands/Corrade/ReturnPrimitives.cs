using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> returnprimitives =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    UUID agentUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT)), message)),
                            out agentUUID) && !AgentNameToUUID(
                                wasInput(
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME)),
                                        message)),
                                wasInput(
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME)),
                                        message)),
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                ref agentUUID))
                    {
                        throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                    }
                    string region =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REGION)),
                            message));
                    Simulator simulator =
                        Client.Network.Simulators.FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                    StringComparison.InvariantCultureIgnoreCase));
                    if (simulator == null)
                    {
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    }
                    string type =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)),
                            message));
                    switch (
                        wasGetEnumValueFromDescription<Entity>(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ENTITY)),
                                    message)).ToLowerInvariant()))
                    {
                        case Entity.PARCEL:
                            Vector3 position;
                            HashSet<Parcel> parcels = new HashSet<Parcel>();
                            switch (Vector3.TryParse(
                                wasInput(
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION)),
                                        message)),
                                out position))
                            {
                                case false:
                                    // Get all sim parcels
                                    ManualResetEvent SimParcelsDownloadedEvent = new ManualResetEvent(false);
                                    EventHandler<SimParcelsDownloadedEventArgs> SimParcelsDownloadedEventHandler =
                                        (sender, args) => SimParcelsDownloadedEvent.Set();
                                    lock (ClientInstanceParcelsLock)
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
                                    if (!GetParcelAtPosition(simulator, position, ref parcel))
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
                                Parallel.ForEach(
                                    parcels.AsParallel().Where(o => !o.OwnerID.Equals(Client.Self.AgentID)), o =>
                                    {
                                        if (!o.IsGroupOwned || !o.GroupID.Equals(commandGroup.UUID))
                                        {
                                            throw new Exception(
                                                wasGetDescriptionFromEnumValue(
                                                    ScriptError.NO_GROUP_POWER_FOR_COMMAND));
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
                                        if (!HasGroupPowers(Client.Self.AgentID, commandGroup.UUID, power,
                                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                                        {
                                            throw new Exception(
                                                wasGetDescriptionFromEnumValue(
                                                    ScriptError.NO_GROUP_POWER_FOR_COMMAND));
                                        }
                                    });
                            }
                            Parallel.ForEach(parcels,
                                o =>
                                    Client.Parcels.ReturnObjects(simulator, o.LocalID,
                                        returnType
                                        , new List<UUID> {agentUUID}));

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
                                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ALL)),
                                            message)),
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
                            Client.Estate.SimWideReturn(agentUUID, estateReturnFlagsField != null
                                ? (EstateTools.EstateReturnFlags)
                                    estateReturnFlagsField
                                        .GetValue(null)
                                : EstateTools.EstateReturnFlags.ReturnScriptedAndOnOthers, allEstates);
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                    }
                };
        }
    }
}