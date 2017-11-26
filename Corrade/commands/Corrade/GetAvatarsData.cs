///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getavatarsdata =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                            out range))
                        range = corradeConfiguration.Range;
                    var avatars = new HashSet<Avatar>();
                    var LockObject = new object();
                    switch (Reflection.GetEnumValueFromName<Enumerations.Entity>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message))
                    ))
                    {
                        case Enumerations.Entity.RANGE:
                            Services.GetAvatars(Client, range)
                                .AsParallel()
                                .Where(o => Vector3.Distance(o.Position, Client.Self.SimPosition) <= range).ForAll(
                                    o =>
                                    {
                                        lock (LockObject)
                                        {
                                            avatars.Add(o);
                                        }
                                    });
                            break;

                        case Enumerations.Entity.PARCEL:
                            Vector3 position;
                            if (
                                !Vector3.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                            corradeCommandParameters.Message)),
                                    out position))
                            {
                                Locks.ClientInstanceSelfLock.EnterReadLock();
                                position = Client.Self.SimPosition;
                                Locks.ClientInstanceSelfLock.ExitReadLock();
                            }
                            Parcel parcel = null;
                            if (
                                !Services.GetParcelAtPosition(Client, Client.Network.CurrentSim, position,
                                    corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout, ref parcel))
                                throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                            Services.GetAvatars(Client, new[]
                                {
                                    Vector3.Distance(Client.Self.SimPosition, parcel.AABBMin),
                                    Vector3.Distance(Client.Self.SimPosition, parcel.AABBMax),
                                    Vector3.Distance(Client.Self.SimPosition,
                                        new Vector3(parcel.AABBMin.X, parcel.AABBMax.Y, 0)),
                                    Vector3.Distance(Client.Self.SimPosition,
                                        new Vector3(parcel.AABBMax.X, parcel.AABBMin.Y, 0))
                                }.Max())
                                .AsParallel()
                                .Where(o => wasOpenMetaverse.Helpers.IsVectorInParcel(o.Position, parcel)).ForAll(o =>
                                {
                                    lock (LockObject)
                                    {
                                        avatars.Add(o);
                                    }
                                });
                            break;

                        case Enumerations.Entity.REGION:
                            // Get all sim parcels
                            var SimParcelsDownloadedEvent = new ManualResetEventSlim(false);
                            EventHandler<SimParcelsDownloadedEventArgs> SimParcelsDownloadedEventHandler =
                                (sender, args) => SimParcelsDownloadedEvent.Set();
                            Locks.ClientInstanceParcelsLock.EnterReadLock();
                            Client.Parcels.SimParcelsDownloaded += SimParcelsDownloadedEventHandler;
                            Client.Parcels.RequestAllSimParcels(Client.Network.CurrentSim, true,
                                (int) corradeConfiguration.DataTimeout);
                            if (Client.Network.CurrentSim.IsParcelMapFull())
                                SimParcelsDownloadedEvent.Set();
                            if (
                                !SimParcelsDownloadedEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                            {
                                Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                                Locks.ClientInstanceParcelsLock.ExitReadLock();
                                throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_PARCELS);
                            }
                            Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                            Locks.ClientInstanceParcelsLock.ExitReadLock();
                            var regionParcels =
                                new HashSet<Parcel>(Client.Network.CurrentSim.Parcels.Copy().Values);
                            Services.GetAvatars(Client,
                                    regionParcels.AsParallel().Select(o => new[]
                                    {
                                        Vector3.Distance(Client.Self.SimPosition, o.AABBMin),
                                        Vector3.Distance(Client.Self.SimPosition, o.AABBMax),
                                        Vector3.Distance(Client.Self.SimPosition,
                                            new Vector3(o.AABBMin.X, o.AABBMax.Y, 0)),
                                        Vector3.Distance(Client.Self.SimPosition,
                                            new Vector3(o.AABBMax.X, o.AABBMin.Y, 0))
                                    }.Max()).Max())
                                .AsParallel()
                                .Where(
                                    o =>
                                        regionParcels
                                            .AsParallel()
                                            .Any(p => wasOpenMetaverse.Helpers.IsVectorInParcel(o.Position, p)))
                                .ForAll(
                                    o =>
                                    {
                                        lock (LockObject)
                                        {
                                            avatars.Add(o);
                                        }
                                    });
                            break;

                        case Enumerations.Entity.AVATAR:
                            UUID agentUUID;
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                            corradeCommandParameters.Message)), out agentUUID) &&
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
                                    ref agentUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                            var avatar = Services.GetAvatars(Client, range)
                                .AsParallel()
                                .FirstOrDefault(o => o.ID.Equals(agentUUID));
                            if (avatar == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.AVATAR_NOT_IN_RANGE);
                            avatars.Add(avatar);
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }

                    // allow partial results
                    Services.UpdateAvatars(Client, ref avatars, corradeConfiguration.ServicesTimeout,
                        corradeConfiguration.DataTimeout,
                        new DecayingAlarm(corradeConfiguration.DataDecayType));

                    var data = new List<string>();

                    avatars.AsParallel().ForAll(o =>
                    {
                        var avatarData = o.GetStructuredData(wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                corradeCommandParameters.Message))).ToList();
                        if (avatarData.Any())
                            lock (LockObject)
                            {
                                data.AddRange(avatarData);
                            }
                    });
                    if (data.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                };
        }
    }
}