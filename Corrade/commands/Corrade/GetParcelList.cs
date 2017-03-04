///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using CorradeConfigurationSharp;
using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using wasOpenMetaverse;
using wasSharp;
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
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int)Configuration.Permissions.Land))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                    {
                        position = Client.Self.SimPosition;
                    }
                    var region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
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
                        throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                    }
                    Parcel parcel = null;
                    if (
                        !Services.GetParcelAtPosition(Client, simulator, position, corradeConfiguration.ServicesTimeout,
                            ref parcel))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                    }
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
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACCESS_LIST_TYPE);
                    }

                    var accessType = (AccessList)accessField.GetValue(null);

                    var random = new Random().Next();
                    List<ParcelManager.ParcelAccessEntry> accessList = new List<ParcelManager.ParcelAccessEntry>();
                    wasSharp.Timers.DecayingAlarm ParcelAccessListAlarm = new wasSharp.Timers.DecayingAlarm(corradeConfiguration.DataDecayType);
                    EventHandler<ParcelAccessListReplyEventArgs> ParcelAccessListHandler = (sender, args) =>
                    {
                        if (!args.LocalID.Equals(parcel.LocalID)) return;

                        ParcelAccessListAlarm.Alarm(corradeConfiguration.DataTimeout);
                        if (args.AccessList != null && args.AccessList.Any())
                            accessList.AddRange(args.AccessList);
                    };
                    lock (Locks.ClientInstanceParcelsLock)
                    {
                        Client.Parcels.ParcelAccessListReply += ParcelAccessListHandler;
                        Client.Parcels.RequestParcelAccessList(simulator, parcel.LocalID, accessType, random);
                        if (!ParcelAccessListAlarm.Signal.WaitOne((int)corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Parcels.ParcelAccessListReply -= ParcelAccessListHandler;
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_PARCEL_LIST);
                        }
                        Client.Parcels.ParcelAccessListReply -= ParcelAccessListHandler;
                    }

                    var csv = new List<string>();
                    var LockObject = new object();
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
                    {
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                    }
                };
        }
    }
}
