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
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getprimitiveowners =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var region =
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
                    Vector3 position;
                    var parcels = new HashSet<Parcel>();
                    switch (Vector3.TryParse(
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION)),
                            corradeCommandParameters.Message)),
                        out position))
                    {
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
                        default:
                            // Get all sim parcels
                            var SimParcelsDownloadedEvent = new ManualResetEvent(false);
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
                                    !SimParcelsDownloadedEvent.WaitOne((int) corradeConfiguration.ServicesTimeout,
                                        false))
                                {
                                    Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_GETTING_PARCELS);
                                }
                                Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                            }
                            simulator.Parcels.ForEach(o => parcels.Add(o));
                            break;
                    }
                    var succeeded = true;
                    Parallel.ForEach(parcels.AsParallel().Where(o => !o.OwnerID.Equals(Client.Self.AgentID)),
                        (o, state) =>
                        {
                            if (!o.IsGroupOwned || !o.GroupID.Equals(corradeCommandParameters.Group.UUID))
                            {
                                succeeded = false;
                                state.Break();
                            }
                            var permissions = false;
                            Parallel.ForEach(
                                new HashSet<GroupPowers>
                                {
                                    GroupPowers.ReturnGroupSet,
                                    GroupPowers.ReturnGroupOwned,
                                    GroupPowers.ReturnNonGroup
                                }, (p, s) =>
                                {
                                    if (Services.HasGroupPowers(Client, Client.Self.AgentID,
                                        corradeCommandParameters.Group.UUID, p,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                                    {
                                        permissions = true;
                                        s.Break();
                                    }
                                });
                            if (!permissions)
                            {
                                succeeded = false;
                                state.Break();
                            }
                        });
                    if (!succeeded) throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    var primitives = new Dictionary<UUID, int>();
                    var LockObject = new object();
                    foreach (var parcel in parcels)
                    {
                        var ParcelObjectOwnersReplyEvent = new ManualResetEvent(false);
                        List<ParcelManager.ParcelPrimOwners> parcelPrimOwners = null;
                        EventHandler<ParcelObjectOwnersReplyEventArgs> ParcelObjectOwnersEventHandler =
                            (sender, args) =>
                            {
                                parcelPrimOwners = args.PrimOwners;
                                ParcelObjectOwnersReplyEvent.Set();
                            };
                        lock (Locks.ClientInstanceParcelsLock)
                        {
                            Client.Parcels.ParcelObjectOwnersReply += ParcelObjectOwnersEventHandler;
                            Client.Parcels.RequestObjectOwners(simulator, parcel.LocalID);
                            if (
                                !ParcelObjectOwnersReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout,
                                    false))
                            {
                                Client.Parcels.ParcelObjectOwnersReply -= ParcelObjectOwnersEventHandler;
                                throw new ScriptException(ScriptError.TIMEOUT_GETTING_LAND_USERS);
                            }
                            Client.Parcels.ParcelObjectOwnersReply -= ParcelObjectOwnersEventHandler;
                            parcelPrimOwners.AsParallel().ForAll(o =>
                            {
                                lock (LockObject)
                                {
                                    switch (primitives.ContainsKey(o.OwnerID))
                                    {
                                        case true:
                                            primitives[o.OwnerID] += o.Count;
                                            break;
                                        default:
                                            primitives.Add(o.OwnerID, o.Count);
                                            break;
                                    }
                                }
                            });
                        }
                    }
                    var csv = new List<string>();
                    primitives.AsParallel().ForAll(o =>
                    {
                        var owner = string.Empty;
                        if (!Resolvers.AgentUUIDToName(Client, o.Key, corradeConfiguration.ServicesTimeout, ref owner))
                            return;
                        lock (LockObject)
                        {
                            csv.AddRange(new[]
                            {
                                owner,
                                o.Key.ToString(),
                                o.Value.ToString(Utils.EnUsCulture)
                            });
                        }
                    });
                    if (csv.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA), CSV.FromEnumerable(csv));
                    }
                };
        }
    }
}