///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getestatelist =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Configuration.Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    if (!Client.Network.CurrentSim.IsEstateManager)
                    {
                        throw new ScriptException(ScriptError.NO_LAND_RIGHTS);
                    }
                    List<UUID> estateList = new List<UUID>();
                    Time.DecayingAlarm EstateListReceivedAlarm =
                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType);
                    Type type =
                        Reflection.GetEnumValueFromName<Type>(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                                corradeCommandParameters.Message))
                                .ToLowerInvariant());
                    switch (type)
                    {
                        case Type.BAN:
                            EventHandler<EstateBansReplyEventArgs> EstateBansReplyEventHandler = (sender, args) =>
                            {
                                EstateListReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                                switch (args.Banned.Any())
                                {
                                    case true:
                                        estateList.AddRange(args.Banned);
                                        break;
                                    default:
                                        EstateListReceivedAlarm.Signal.Set();
                                        break;
                                }
                            };
                            lock (ClientInstanceEstateLock)
                            {
                                Client.Estate.EstateBansReply += EstateBansReplyEventHandler;
                                Client.Estate.RequestInfo();
                                if (
                                    !EstateListReceivedAlarm.Signal.WaitOne(
                                        (int) corradeConfiguration.ServicesTimeout,
                                        false))
                                {
                                    Client.Estate.EstateBansReply -= EstateBansReplyEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_RETRIEVING_ESTATE_LIST);
                                }
                                Client.Estate.EstateBansReply -= EstateBansReplyEventHandler;
                            }
                            break;
                        case Type.GROUP:
                            EventHandler<EstateGroupsReplyEventArgs> EstateGroupsReplyEvenHandler =
                                (sender, args) =>
                                {
                                    EstateListReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                                    switch (args.AllowedGroups.Any())
                                    {
                                        case true:
                                            estateList.AddRange(args.AllowedGroups);
                                            break;
                                        default:
                                            EstateListReceivedAlarm.Signal.Set();
                                            break;
                                    }
                                };
                            lock (ClientInstanceEstateLock)
                            {
                                Client.Estate.EstateGroupsReply += EstateGroupsReplyEvenHandler;
                                Client.Estate.RequestInfo();
                                if (
                                    !EstateListReceivedAlarm.Signal.WaitOne(
                                        (int) corradeConfiguration.ServicesTimeout,
                                        false))
                                {
                                    Client.Estate.EstateGroupsReply -= EstateGroupsReplyEvenHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_RETRIEVING_ESTATE_LIST);
                                }
                                Client.Estate.EstateGroupsReply -= EstateGroupsReplyEvenHandler;
                            }
                            break;
                        case Type.MANAGER:
                            EventHandler<EstateManagersReplyEventArgs> EstateManagersReplyEventHandler =
                                (sender, args) =>
                                {
                                    EstateListReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                                    switch (args.Managers.Any())
                                    {
                                        case true:
                                            estateList.AddRange(args.Managers);
                                            break;
                                        default:
                                            EstateListReceivedAlarm.Signal.Set();
                                            break;
                                    }
                                };
                            lock (ClientInstanceEstateLock)
                            {
                                Client.Estate.EstateManagersReply += EstateManagersReplyEventHandler;
                                Client.Estate.RequestInfo();
                                if (
                                    !EstateListReceivedAlarm.Signal.WaitOne(
                                        (int) corradeConfiguration.ServicesTimeout,
                                        false))
                                {
                                    Client.Estate.EstateManagersReply -= EstateManagersReplyEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_RETRIEVING_ESTATE_LIST);
                                }
                                Client.Estate.EstateManagersReply -= EstateManagersReplyEventHandler;
                            }
                            break;
                        case Type.USER:
                            EventHandler<EstateUsersReplyEventArgs> EstateUsersReplyEventHandler =
                                (sender, args) =>
                                {
                                    EstateListReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                                    switch (args.AllowedUsers.Any())
                                    {
                                        case true:
                                            estateList.AddRange(args.AllowedUsers);
                                            break;
                                        default:
                                            EstateListReceivedAlarm.Signal.Set();
                                            break;
                                    }
                                };
                            lock (ClientInstanceEstateLock)
                            {
                                Client.Estate.EstateUsersReply += EstateUsersReplyEventHandler;
                                Client.Estate.RequestInfo();
                                if (
                                    !EstateListReceivedAlarm.Signal.WaitOne(
                                        (int) corradeConfiguration.ServicesTimeout,
                                        false))
                                {
                                    Client.Estate.EstateUsersReply -= EstateUsersReplyEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_RETRIEVING_ESTATE_LIST);
                                }
                                Client.Estate.EstateUsersReply -= EstateUsersReplyEventHandler;
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ESTATE_LIST);
                    }
                    // resolve UUIDs to names
                    object LockObject = new object();
                    List<string> csv = new List<string>();
                    switch (type)
                    {
                        case Type.BAN:
                        case Type.MANAGER:
                        case Type.USER:
                            Parallel.ForEach(estateList, o =>
                            {
                                string agentName = string.Empty;
                                if (!AgentUUIDToName(o, corradeConfiguration.ServicesTimeout, ref agentName))
                                    return;
                                lock (LockObject)
                                {
                                    csv.Add(agentName);
                                    csv.Add(o.ToString());
                                }
                            });
                            break;
                        case Type.GROUP:
                            Parallel.ForEach(estateList, o =>
                            {
                                string groupName = string.Empty;
                                if (!GroupUUIDToName(o, corradeConfiguration.ServicesTimeout, ref groupName))
                                    return;
                                lock (LockObject)
                                {
                                    csv.Add(groupName);
                                    csv.Add(o.ToString());
                                }
                            });
                            break;
                    }
                    if (csv.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                    }
                };
        }
    }
}