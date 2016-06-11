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

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getobjectsdata =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
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
                    var updatePrimitives = new HashSet<Primitive>();
                    var LockObject = new object();
                    switch (Reflection.GetEnumValueFromName<Entity>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Entity.RANGE:
                            updatePrimitives = Services.GetObjects(Client, range);
                            break;
                        case Entity.WORLD:
                            var avatars =
                                new HashSet<uint>(Services.GetAvatars(Client, range).Select(o => o.LocalID));
                            updatePrimitives =
                                new HashSet<Primitive>(
                                    Services.GetObjects(Client, range)
                                        .AsParallel()
                                        .Where(o => o.ParentID.Equals(0) && !avatars.Contains(o.ParentID)));
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
                            updatePrimitives = Services.GetObjects(Client, new[]
                            {
                                Vector3.Distance(Client.Self.SimPosition, parcel.AABBMin),
                                Vector3.Distance(Client.Self.SimPosition, parcel.AABBMax),
                                Vector3.Distance(Client.Self.SimPosition,
                                    new Vector3(parcel.AABBMin.X, parcel.AABBMax.Y, 0)),
                                Vector3.Distance(Client.Self.SimPosition,
                                    new Vector3(parcel.AABBMax.X, parcel.AABBMin.Y, 0))
                            }.Max());
                            break;
                        case Entity.REGION:
                            // Get all sim parcels
                            var SimParcelsDownloadedEvent = new ManualResetEvent(false);
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
                            updatePrimitives = Services.GetObjects(Client,
                                Client.Network.CurrentSim.Parcels.Copy().Values.AsParallel().Select(o => new[]
                                {
                                    Vector3.Distance(Client.Self.SimPosition, o.AABBMin),
                                    Vector3.Distance(Client.Self.SimPosition, o.AABBMax),
                                    Vector3.Distance(Client.Self.SimPosition,
                                        new Vector3(o.AABBMin.X, o.AABBMax.Y, 0)),
                                    Vector3.Distance(Client.Self.SimPosition,
                                        new Vector3(o.AABBMax.X, o.AABBMin.Y, 0))
                                }.Max()).Max());
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
                            var avatar = Services.GetAvatars(Client, range)
                                .AsParallel()
                                .FirstOrDefault(o => o.ID.Equals(agentUUID));
                            if (avatar == null)
                                throw new ScriptException(ScriptError.AVATAR_NOT_IN_RANGE);
                            var objectsPrimitives =
                                new HashSet<Primitive>(Services.GetObjects(Client, range));
                            objectsPrimitives.AsParallel().ForAll(
                                o =>
                                {
                                    switch (!o.ParentID.Equals(avatar.LocalID))
                                    {
                                        case true:
                                            var primitiveParent =
                                                objectsPrimitives.AsParallel()
                                                    .FirstOrDefault(p => p.LocalID.Equals(o.ParentID));
                                            if (primitiveParent != null &&
                                                primitiveParent.ParentID.Equals(avatar.LocalID))
                                            {
                                                lock (LockObject)
                                                {
                                                    updatePrimitives.Add(o);
                                                }
                                            }
                                            break;
                                        default:
                                            lock (LockObject)
                                            {
                                                updatePrimitives.Add(o);
                                            }
                                            break;
                                    }
                                });
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                    }

                    // allow partial results
                    Services.UpdatePrimitives(Client, ref updatePrimitives, corradeConfiguration.DataTimeout);

                    var data = new List<string>();
                    var dataQuery = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                            corradeCommandParameters.Message));
                    updatePrimitives.AsParallel().ForAll(o =>
                    {
                        var primitiveData = GetStructuredData(o, dataQuery).ToList();
                        if (primitiveData.Any())
                        {
                            lock (LockObject)
                            {
                                data.AddRange(primitiveData);
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