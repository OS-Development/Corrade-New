///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getestatelist =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int) Configuration.Permissions.Land))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    if (!Client.Network.CurrentSim.IsEstateManager)
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_LAND_RIGHTS);
                    var estateList = new List<UUID>();
                    var EstateListReceivedAlarm =
                        new DecayingAlarm(corradeConfiguration.DataDecayType);
                    var type =
                        Reflection.GetEnumValueFromName<Enumerations.Type>(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                corradeCommandParameters.Message))
                        );
                    switch (type)
                    {
                        case Enumerations.Type.BAN:
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
                            Locks.ClientInstanceEstateLock.EnterWriteLock();
                            Client.Estate.EstateBansReply += EstateBansReplyEventHandler;
                            Client.Estate.RequestInfo();
                            if (
                                !EstateListReceivedAlarm.Signal.WaitOne(
                                    (int) corradeConfiguration.ServicesTimeout,
                                    false))
                            {
                                Client.Estate.EstateBansReply -= EstateBansReplyEventHandler;
                                Locks.ClientInstanceEstateLock.ExitWriteLock();
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_RETRIEVING_ESTATE_LIST);
                            }
                            Client.Estate.EstateBansReply -= EstateBansReplyEventHandler;
                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                            break;

                        case Enumerations.Type.GROUP:
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
                            Locks.ClientInstanceEstateLock.EnterWriteLock();
                            Client.Estate.EstateGroupsReply += EstateGroupsReplyEvenHandler;
                            Client.Estate.RequestInfo();
                            if (
                                !EstateListReceivedAlarm.Signal.WaitOne(
                                    (int) corradeConfiguration.ServicesTimeout,
                                    false))
                            {
                                Client.Estate.EstateGroupsReply -= EstateGroupsReplyEvenHandler;
                                Locks.ClientInstanceEstateLock.ExitWriteLock();
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_RETRIEVING_ESTATE_LIST);
                            }
                            Client.Estate.EstateGroupsReply -= EstateGroupsReplyEvenHandler;
                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                            break;

                        case Enumerations.Type.MANAGER:
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
                            Locks.ClientInstanceEstateLock.EnterWriteLock();
                            Client.Estate.EstateManagersReply += EstateManagersReplyEventHandler;
                            Client.Estate.RequestInfo();
                            if (
                                !EstateListReceivedAlarm.Signal.WaitOne(
                                    (int) corradeConfiguration.ServicesTimeout,
                                    false))
                            {
                                Client.Estate.EstateManagersReply -= EstateManagersReplyEventHandler;
                                Locks.ClientInstanceEstateLock.ExitWriteLock();
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_RETRIEVING_ESTATE_LIST);
                            }
                            Client.Estate.EstateManagersReply -= EstateManagersReplyEventHandler;
                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                            break;

                        case Enumerations.Type.USER:
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
                            Locks.ClientInstanceEstateLock.EnterWriteLock();
                            Client.Estate.EstateUsersReply += EstateUsersReplyEventHandler;
                            Client.Estate.RequestInfo();
                            if (
                                !EstateListReceivedAlarm.Signal.WaitOne(
                                    (int) corradeConfiguration.ServicesTimeout,
                                    false))
                            {
                                Client.Estate.EstateUsersReply -= EstateUsersReplyEventHandler;
                                Locks.ClientInstanceEstateLock.ExitWriteLock();
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_RETRIEVING_ESTATE_LIST);
                            }
                            Client.Estate.EstateUsersReply -= EstateUsersReplyEventHandler;
                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ESTATE_LIST);
                    }
                    // resolve UUIDs to names
                    var LockObject = new object();
                    var csv = new List<string>();
                    switch (type)
                    {
                        case Enumerations.Type.BAN:
                        case Enumerations.Type.MANAGER:
                        case Enumerations.Type.USER:
                            estateList.AsParallel().ForAll(o =>
                            {
                                var agentName = string.Empty;
                                if (
                                    !Resolvers.AgentUUIDToName(Client, o, corradeConfiguration.ServicesTimeout,
                                        ref agentName))
                                    return;
                                lock (LockObject)
                                {
                                    csv.Add(agentName);
                                    csv.Add(o.ToString());
                                }
                            });
                            break;

                        case Enumerations.Type.GROUP:
                            estateList.AsParallel().ForAll(o =>
                            {
                                var groupName = string.Empty;
                                if (
                                    !Resolvers.GroupUUIDToName(Client, o, corradeConfiguration.ServicesTimeout,
                                        ref groupName))
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
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                };
        }
    }
}