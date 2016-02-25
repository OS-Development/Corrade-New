///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Helpers = wasOpenMetaverse.Helpers;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getavatarsdata =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)),
                            out range))
                    {
                        range = corradeConfiguration.Range;
                    }
                    HashSet<Avatar> avatars = new HashSet<Avatar>();
                    object LockObject = new object();
                    switch (Reflection.GetEnumValueFromName<Entity>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Entity.RANGE:
                            Parallel.ForEach(
                                Services.GetAvatars(Client, range, corradeConfiguration.Range,
                                    corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout,
                                    new Time.DecayingAlarm(corradeConfiguration.DataDecayType))
                                    .AsParallel()
                                    .Where(o => Vector3.Distance(o.Position, Client.Self.SimPosition) <= range),
                                o =>
                                {
                                    lock (LockObject)
                                    {
                                        avatars.Add(o);
                                    }
                                });
                            break;
                        case Entity.PARCEL:
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
                            Parcel parcel = null;
                            if (
                                !Services.GetParcelAtPosition(Client, Client.Network.CurrentSim, position,
                                    corradeConfiguration.ServicesTimeout, ref parcel))
                            {
                                throw new ScriptException(ScriptError.COULD_NOT_FIND_PARCEL);
                            }
                            Parallel.ForEach(Services.GetAvatars(Client, new[]
                            {
                                Vector3.Distance(Client.Self.SimPosition, parcel.AABBMin),
                                Vector3.Distance(Client.Self.SimPosition, parcel.AABBMax),
                                Vector3.Distance(Client.Self.SimPosition,
                                    new Vector3(parcel.AABBMin.X, parcel.AABBMax.Y, 0)),
                                Vector3.Distance(Client.Self.SimPosition,
                                    new Vector3(parcel.AABBMax.X, parcel.AABBMin.Y, 0))
                            }.Max(), corradeConfiguration.Range, corradeConfiguration.ServicesTimeout,
                                corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType))
                                .AsParallel()
                                .Where(o => Helpers.IsVectorInParcel(o.Position, parcel)), o =>
                                {
                                    lock (LockObject)
                                    {
                                        avatars.Add(o);
                                    }
                                });
                            break;
                        case Entity.REGION:
                            // Get all sim parcels
                            ManualResetEvent SimParcelsDownloadedEvent = new ManualResetEvent(false);
                            EventHandler<SimParcelsDownloadedEventArgs> SimParcelsDownloadedEventHandler =
                                (sender, args) => SimParcelsDownloadedEvent.Set();
                            lock (Locks.ClientInstanceParcelsLock)
                            {
                                Client.Parcels.SimParcelsDownloaded += SimParcelsDownloadedEventHandler;
                                Client.Parcels.RequestAllSimParcels(Client.Network.CurrentSim);
                                if (Client.Network.CurrentSim.IsParcelMapFull())
                                {
                                    SimParcelsDownloadedEvent.Set();
                                }
                                if (
                                    !SimParcelsDownloadedEvent.WaitOne((int) corradeConfiguration.ServicesTimeout,
                                        false))
                                {
                                    Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_GETTING_PARCELS);
                                }
                                Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                            }
                            HashSet<Parcel> regionParcels =
                                new HashSet<Parcel>(Client.Network.CurrentSim.Parcels.Copy().Values);
                            Parallel.ForEach(Services.GetAvatars(Client,
                                regionParcels.AsParallel().Select(o => new[]
                                {
                                    Vector3.Distance(Client.Self.SimPosition, o.AABBMin),
                                    Vector3.Distance(Client.Self.SimPosition, o.AABBMax),
                                    Vector3.Distance(Client.Self.SimPosition,
                                        new Vector3(o.AABBMin.X, o.AABBMax.Y, 0)),
                                    Vector3.Distance(Client.Self.SimPosition,
                                        new Vector3(o.AABBMax.X, o.AABBMin.Y, 0))
                                }.Max()).Max(), corradeConfiguration.Range, corradeConfiguration.ServicesTimeout,
                                corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType))
                                .AsParallel()
                                .Where(o => regionParcels.AsParallel().Any(p => Helpers.IsVectorInParcel(o.Position, p))),
                                o =>
                                {
                                    lock (LockObject)
                                    {
                                        avatars.Add(o);
                                    }
                                });
                            break;
                        case Entity.AVATAR:
                            UUID agentUUID;
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT)),
                                            corradeCommandParameters.Message)), out agentUUID) &&
                                !Resolvers.AgentNameToUUID(Client,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(
                                                Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME)),
                                            corradeCommandParameters.Message)),
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME)),
                                            corradeCommandParameters.Message)),
                                    corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout,
                                    new Time.DecayingAlarm(corradeConfiguration.DataDecayType),
                                    ref agentUUID))
                            {
                                throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                            }
                            Avatar avatar = Services.GetAvatars(Client, range, corradeConfiguration.Range,
                                corradeConfiguration.ServicesTimeout,
                                corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType))
                                .AsParallel()
                                .FirstOrDefault(o => o.ID.Equals(agentUUID));
                            if (avatar == null)
                                throw new ScriptException(ScriptError.AVATAR_NOT_IN_RANGE);
                            avatars.Add(avatar);
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                    }

                    // allow partial results
                    Services.UpdateAvatars(Client, ref avatars, corradeConfiguration.ServicesTimeout,
                        corradeConfiguration.DataTimeout, new Time.DecayingAlarm(corradeConfiguration.DataDecayType));

                    List<string> data = new List<string>();

                    Parallel.ForEach(avatars, o =>
                    {
                        List<string> avatarData = GetStructuredData(o,
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                    corradeCommandParameters.Message))).ToList();
                        if (avatarData.Any())
                        {
                            lock (LockObject)
                            {
                                data.AddRange(avatarData);
                            }
                        }
                    });
                    if (data.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                    }
                };
        }
    }
}