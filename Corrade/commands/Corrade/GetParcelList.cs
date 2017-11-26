///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getparcellist =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int) Configuration.Permissions.Land))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                        position = Client.Self.SimPosition;
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
                    Parcel parcel = null;
                    if (
                        !Services.GetParcelAtPosition(Client, simulator, position, corradeConfiguration.ServicesTimeout,
                            corradeConfiguration.DataTimeout,
                            ref parcel))
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                    var accessField = typeof(AccessList).GetFields(
                            BindingFlags.Public | BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                            corradeCommandParameters.Message)),
                                    StringComparison.Ordinal));
                    if (accessField == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACCESS_LIST_TYPE);

                    var accessType = (AccessList) accessField.GetValue(null);

                    var accessList = new List<ParcelManager.ParcelAccessEntry>();
                    var ParcelAccessListAlarm = new DecayingAlarm(corradeConfiguration.DataDecayType);
                    var LockObject = new object();
                    EventHandler<ParcelAccessListReplyEventArgs> ParcelAccessListHandler = (sender, args) =>
                    {
                        if (!args.LocalID.Equals(parcel.LocalID) ||
                            !args.Simulator.RegionID.Equals(simulator.RegionID)) return;

                        ParcelAccessListAlarm.Alarm(corradeConfiguration.DataTimeout);
                        if (args.AccessList != null && args.AccessList.Any())
                            lock (LockObject)
                            {
                                accessList.AddRange(args.AccessList);
                            }
                    };
                    Locks.ClientInstanceParcelsLock.EnterReadLock();
                    Client.Parcels.ParcelAccessListReply += ParcelAccessListHandler;
                    Client.Parcels.RequestParcelAccessList(simulator, parcel.LocalID, accessType, 0);
                    if (!ParcelAccessListAlarm.Signal.WaitOne((int) corradeConfiguration.ServicesTimeout, true))
                    {
                        Client.Parcels.ParcelAccessListReply -= ParcelAccessListHandler;
                        Locks.ClientInstanceParcelsLock.ExitReadLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_PARCEL_LIST);
                    }
                    Client.Parcels.ParcelAccessListReply -= ParcelAccessListHandler;
                    Locks.ClientInstanceParcelsLock.ExitReadLock();

                    var csv = new List<string>();
                    accessList.AsParallel().ForAll(o =>
                    {
                        var agent = string.Empty;
                        if (
                            !Resolvers.AgentUUIDToName(Client, o.AgentID, corradeConfiguration.ServicesTimeout,
                                ref agent))
                            return;
                        lock (LockObject)
                        {
                            csv.Add(agent);
                            csv.Add(o.AgentID.ToString());
                            csv.Add(o.Flags.ToString());
                            csv.Add(o.Time.ToString(Utils.EnUsCulture));
                        }
                    });

                    if (csv.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                };
        }
    }
}