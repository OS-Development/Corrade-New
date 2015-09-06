///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> getestatelist =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    if (!Client.Network.CurrentSim.IsEstateManager)
                    {
                        throw new ScriptException(ScriptError.NO_LAND_RIGHTS);
                    }
                    List<UUID> estateList = new List<UUID>();
                    wasAdaptiveAlarm EstateListReceivedAlarm =
                        new wasAdaptiveAlarm(corradeConfiguration.DataDecayType);
                    switch (
                        wasGetEnumValueFromDescription<Type>(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)), message))
                                .ToLowerInvariant()))
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
                    List<string> data = new List<string>(estateList.ConvertAll(o => o.ToString()));
                    if (data.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(data));
                    }
                };
        }
    }
}