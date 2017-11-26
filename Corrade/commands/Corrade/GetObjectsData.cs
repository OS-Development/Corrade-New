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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getobjectsdata =
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
                    var updateObjects = new HashSet<Primitive>();
                    var LockObject = new object();
                    switch (Reflection.GetEnumValueFromName<Enumerations.Entity>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message))
                    ))
                    {
                        case Enumerations.Entity.RANGE:
                            updateObjects = Services.GetObjects(Client, range);
                            break;

                        case Enumerations.Entity.WORLD:
                            var avatars =
                                new HashSet<uint>(Services.GetAvatars(Client, range).Select(o => o.LocalID));
                            updateObjects =
                                new HashSet<Primitive>(
                                    Services.GetObjects(Client, range)
                                        .AsParallel()
                                        .Where(o => o.ParentID.Equals(0) && !avatars.Contains(o.ParentID)));
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
                                position = Client.Self.SimPosition;
                            Parcel parcel = null;
                            if (
                                !Services.GetParcelAtPosition(Client, Client.Network.CurrentSim, position,
                                    corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout, ref parcel))
                                throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                            updateObjects.UnionWith(Services.GetObjects(Client, new[]
                                {
                                    Vector3.Distance(Client.Self.SimPosition, parcel.AABBMin),
                                    Vector3.Distance(Client.Self.SimPosition, parcel.AABBMax),
                                    Vector3.Distance(Client.Self.SimPosition,
                                        new Vector3(parcel.AABBMin.X, parcel.AABBMax.Y, 0)),
                                    Vector3.Distance(Client.Self.SimPosition,
                                        new Vector3(parcel.AABBMax.X, parcel.AABBMin.Y, 0))
                                }.Max())
                                .AsParallel()
                                .Where(o => o.Position.X >= parcel.AABBMin.X && o.Position.X <= parcel.AABBMax.X &&
                                            o.Position.Y >= parcel.AABBMin.Y &&
                                            o.Position.Y <= parcel.AABBMax.Y));
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
                            updateObjects.UnionWith(Services.GetObjects(Client,
                                Client.Network.CurrentSim.Parcels.Copy().Values.AsParallel().Select(o => new[]
                                {
                                    Vector3.Distance(Client.Self.SimPosition, o.AABBMin),
                                    Vector3.Distance(Client.Self.SimPosition, o.AABBMax),
                                    Vector3.Distance(Client.Self.SimPosition,
                                        new Vector3(o.AABBMin.X, o.AABBMax.Y, 0)),
                                    Vector3.Distance(Client.Self.SimPosition,
                                        new Vector3(o.AABBMax.X, o.AABBMin.Y, 0))
                                }.Max()).Max()));
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
                            var objectsPrimitives = Services.GetObjects(Client, range)
                                .GroupBy(o => o.LocalID)
                                .ToDictionary(o => o.Key, o => o.FirstOrDefault());
                            objectsPrimitives.AsParallel().ForAll(
                                o =>
                                {
                                    switch (!o.Value.ParentID.Equals(avatar.LocalID))
                                    {
                                        case true:
                                            Primitive primitiveParent = null;
                                            if (objectsPrimitives.TryGetValue(o.Value.ParentID, out primitiveParent) &&
                                                primitiveParent.ParentID.Equals(avatar.LocalID))
                                                lock (LockObject)
                                                {
                                                    updateObjects.Add(o.Value);
                                                }
                                            break;

                                        default:
                                            lock (LockObject)
                                            {
                                                updateObjects.Add(o.Value);
                                            }
                                            break;
                                    }
                                });
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }

                    // allow partial results
                    Services.UpdatePrimitives(Client, ref updateObjects, corradeConfiguration.DataTimeout);

                    var data = new List<string>();
                    var dataQuery = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                            corradeCommandParameters.Message));
                    updateObjects.AsParallel().ForAll(o =>
                    {
                        var primitiveData = o.GetStructuredData(dataQuery).ToList();
                        if (primitiveData.Any())
                            lock (LockObject)
                            {
                                data.AddRange(primitiveData);
                            }
                    });
                    if (data.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                };
        }
    }
}