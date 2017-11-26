///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getprimitiveowners =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Land))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        var region =
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                    corradeCommandParameters.Message));
                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                        var simulator =
                            Client.Network.Simulators.AsParallel().FirstOrDefault(
                                o =>
                                    o.Name.Equals(
                                        string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                        StringComparison.OrdinalIgnoreCase));
                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                        if (simulator == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                        Vector3 position;
                        var parcels = new HashSet<Parcel>();
                        switch (Vector3.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                corradeCommandParameters.Message)),
                            out position))
                        {
                            case true:
                                Parcel parcel = null;
                                if (
                                    !Services.GetParcelAtPosition(Client, simulator, position,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        ref parcel))
                                    throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                                parcels.Add(parcel);
                                break;

                            default:
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
                                    !SimParcelsDownloadedEvent.Wait((int) corradeConfiguration.ServicesTimeout))
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
                                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
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
                        if (!succeeded)
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                        var primitives = new Dictionary<UUID, int>();
                        var LockObject = new object();
                        foreach (var parcel in parcels)
                        {
                            var ParcelObjectOwnersReplyEvent = new ManualResetEventSlim(false);
                            List<ParcelManager.ParcelPrimOwners> parcelPrimOwners = null;
                            EventHandler<ParcelObjectOwnersReplyEventArgs> ParcelObjectOwnersEventHandler =
                                (sender, args) =>
                                {
                                    if (!args.Simulator.Handle.Equals(simulator.Handle))
                                        return;

                                    parcelPrimOwners = args.PrimOwners;
                                    ParcelObjectOwnersReplyEvent.Set();
                                };
                            Locks.ClientInstanceParcelsLock.EnterWriteLock();
                            Client.Parcels.ParcelObjectOwnersReply += ParcelObjectOwnersEventHandler;
                            Client.Parcels.RequestObjectOwners(simulator, parcel.LocalID);
                            if (
                                !ParcelObjectOwnersReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                            {
                                Client.Parcels.ParcelObjectOwnersReply -= ParcelObjectOwnersEventHandler;
                                Locks.ClientInstanceParcelsLock.ExitWriteLock();
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_GETTING_LAND_USERS);
                            }
                            Client.Parcels.ParcelObjectOwnersReply -= ParcelObjectOwnersEventHandler;
                            Locks.ClientInstanceParcelsLock.ExitWriteLock();
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
                        var csv = new List<string>();
                        primitives.AsParallel().ForAll(o =>
                        {
                            var owner = string.Empty;
                            if (
                                !Resolvers.AgentUUIDToName(Client, o.Key, corradeConfiguration.ServicesTimeout,
                                    ref owner))
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
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(csv));
                    };
        }
    }
}