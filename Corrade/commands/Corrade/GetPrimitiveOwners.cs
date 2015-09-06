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
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> getprimitiveowners =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
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
                    Vector3 position;
                    HashSet<Parcel> parcels = new HashSet<Parcel>();
                    switch (Vector3.TryParse(
                        wasInput(wasKeyValueGet(
                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION)), message)),
                        out position))
                    {
                        case true:
                            Parcel parcel = null;
                            if (!GetParcelAtPosition(simulator, position, ref parcel))
                            {
                                throw new ScriptException(ScriptError.COULD_NOT_FIND_PARCEL);
                            }
                            parcels.Add(parcel);
                            break;
                        default:
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
                    Parallel.ForEach(parcels.AsParallel().Where(o => !o.OwnerID.Equals(Client.Self.AgentID)),
                        o =>
                        {
                            if (!o.IsGroupOwned || !o.GroupID.Equals(commandGroup.UUID))
                            {
                                throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                            }
                            bool permissions = false;
                            Parallel.ForEach(
                                new HashSet<GroupPowers>
                                {
                                    GroupPowers.ReturnGroupSet,
                                    GroupPowers.ReturnGroupOwned,
                                    GroupPowers.ReturnNonGroup
                                }, p =>
                                {
                                    if (HasGroupPowers(Client.Self.AgentID, commandGroup.UUID, p,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                                    {
                                        permissions = true;
                                    }
                                });
                            if (!permissions)
                            {
                                throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                            }
                        });
                    ManualResetEvent ParcelObjectOwnersReplyEvent = new ManualResetEvent(false);
                    Dictionary<string, int> primitives = new Dictionary<string, int>();
                    EventHandler<ParcelObjectOwnersReplyEventArgs> ParcelObjectOwnersEventHandler =
                        (sender, args) =>
                        {
                            //object LockObject = new object();
                            foreach (ParcelManager.ParcelPrimOwners primowner in args.PrimOwners)
                            {
                                string owner = string.Empty;
                                if (
                                    !AgentUUIDToName(primowner.OwnerID, corradeConfiguration.ServicesTimeout,
                                        ref owner))
                                    continue;
                                if (!primitives.ContainsKey(owner))
                                {
                                    primitives.Add(owner, primowner.Count);
                                    continue;
                                }
                                primitives[owner] += primowner.Count;
                            }
                            ParcelObjectOwnersReplyEvent.Set();
                        };
                    foreach (Parcel parcel in parcels)
                    {
                        lock (ClientInstanceParcelsLock)
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
                        }
                    }
                    if (!primitives.Any())
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_GET_LAND_USERS);
                    }
                    List<string> data = new List<string>(primitives.AsParallel().Select(
                        p =>
                            wasEnumerableToCSV(new[]
                            {p.Key, p.Value.ToString(CultureInfo.DefaultThreadCurrentCulture)})));
                    if (data.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(data));
                    }
                };
        }
    }
}