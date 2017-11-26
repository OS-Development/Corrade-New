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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> setestatelist =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int) Configuration.Permissions.Land))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    if (!Client.Network.CurrentSim.IsEstateManager)
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_LAND_RIGHTS);
                    bool allEstates;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ALL)),
                                    corradeCommandParameters.Message)),
                            out allEstates))
                        allEstates = false;
                    var estateList = new List<UUID>();
                    var EstateListReceivedAlarm =
                        new DecayingAlarm(corradeConfiguration.DataDecayType);
                    UUID targetUUID;
                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Type>(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                corradeCommandParameters.Message))
                        ))
                    {
                        case Enumerations.Type.BAN:
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                            corradeCommandParameters.Message)), out targetUUID) &&
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
                                    ref targetUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                            switch (
                                Reflection.GetEnumValueFromName<Enumerations.Action>(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                            corradeCommandParameters.Message))
                                ))
                            {
                                case Enumerations.Action.ADD:
                                    // if this is SecondLife check that we would not exeed the maximum amount of bans
                                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client))
                                    {
                                        EventHandler<EstateBansReplyEventArgs> EstateBansReplyEventHandler =
                                            (sender, args) =>
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
                                            !EstateListReceivedAlarm.Signal.WaitOne((int)
                                                corradeConfiguration.ServicesTimeout,
                                                false))
                                        {
                                            Client.Estate.EstateBansReply -= EstateBansReplyEventHandler;
                                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.TIMEOUT_RETRIEVING_ESTATE_LIST);
                                        }
                                        Client.Estate.EstateBansReply -= EstateBansReplyEventHandler;
                                        Locks.ClientInstanceEstateLock.ExitWriteLock();
                                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                                        if (estateList.Count >=
                                            wasOpenMetaverse.Constants.ESTATE.MAXIMUM_BAN_LIST_LENGTH)
                                        {
                                            Locks.ClientInstanceNetworkLock.ExitReadLock();
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.MAXIMUM_BAN_LIST_LENGTH_REACHED);
                                        }
                                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                                    }
                                    Locks.ClientInstanceEstateLock.EnterWriteLock();
                                    Client.Estate.BanUser(targetUUID, allEstates);
                                    Locks.ClientInstanceEstateLock.ExitWriteLock();
                                    break;

                                case Enumerations.Action.REMOVE:
                                    Locks.ClientInstanceEstateLock.EnterWriteLock();
                                    Client.Estate.UnbanUser(targetUUID, allEstates);
                                    Locks.ClientInstanceEstateLock.ExitWriteLock();
                                    break;

                                default:
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.UNKNOWN_ESTATE_LIST_ACTION);
                            }
                            break;

                        case Enumerations.Type.GROUP:
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                                            corradeCommandParameters.Message)),
                                    out targetUUID) && !Resolvers.GroupNameToUUID(Client,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                                            corradeCommandParameters.Message)),
                                    corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                    new DecayingAlarm(corradeConfiguration.DataDecayType),
                                    ref targetUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                            switch (
                                Reflection.GetEnumValueFromName<Enumerations.Action>(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                            corradeCommandParameters.Message))
                                ))
                            {
                                case Enumerations.Action.ADD:
                                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client))
                                    {
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
                                            !EstateListReceivedAlarm.Signal.WaitOne((int)
                                                corradeConfiguration.ServicesTimeout, false))
                                        {
                                            Client.Estate.EstateGroupsReply -= EstateGroupsReplyEvenHandler;
                                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.TIMEOUT_RETRIEVING_ESTATE_LIST);
                                        }
                                        Client.Estate.EstateGroupsReply -= EstateGroupsReplyEvenHandler;
                                        Locks.ClientInstanceEstateLock.ExitWriteLock();
                                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                                        if (estateList.Count >=
                                            wasOpenMetaverse.Constants.ESTATE.MAXIMUM_GROUP_LIST_LENGTH)
                                        {
                                            Locks.ClientInstanceNetworkLock.ExitReadLock();
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.MAXIMUM_GROUP_LIST_LENGTH_REACHED);
                                        }
                                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                                    }
                                    Locks.ClientInstanceEstateLock.EnterWriteLock();
                                    Client.Estate.AddAllowedGroup(targetUUID, allEstates);
                                    Locks.ClientInstanceEstateLock.ExitWriteLock();
                                    break;

                                case Enumerations.Action.REMOVE:
                                    Locks.ClientInstanceEstateLock.EnterWriteLock();
                                    Client.Estate.RemoveAllowedGroup(targetUUID, allEstates);
                                    Locks.ClientInstanceEstateLock.ExitWriteLock();
                                    break;

                                default:
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.UNKNOWN_ESTATE_LIST_ACTION);
                            }
                            break;

                        case Enumerations.Type.USER:
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                            corradeCommandParameters.Message)), out targetUUID) &&
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
                                    ref targetUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                            switch (
                                Reflection.GetEnumValueFromName<Enumerations.Action>(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                            corradeCommandParameters.Message))
                                ))
                            {
                                case Enumerations.Action.ADD:
                                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client))
                                    {
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
                                            !EstateListReceivedAlarm.Signal.WaitOne((int)
                                                corradeConfiguration.ServicesTimeout, false))
                                        {
                                            Client.Estate.EstateUsersReply -= EstateUsersReplyEventHandler;
                                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.TIMEOUT_RETRIEVING_ESTATE_LIST);
                                        }
                                        Client.Estate.EstateUsersReply -= EstateUsersReplyEventHandler;
                                        Locks.ClientInstanceEstateLock.ExitWriteLock();
                                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                                        if (estateList.Count >=
                                            wasOpenMetaverse.Constants.ESTATE.MAXIMUM_USER_LIST_LENGTH)
                                        {
                                            Locks.ClientInstanceNetworkLock.ExitReadLock();
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.MAXIMUM_USER_LIST_LENGTH_REACHED);
                                        }
                                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                                    }
                                    Locks.ClientInstanceEstateLock.EnterWriteLock();
                                    Client.Estate.AddAllowedUser(targetUUID, allEstates);
                                    Locks.ClientInstanceEstateLock.ExitWriteLock();
                                    break;

                                case Enumerations.Action.REMOVE:
                                    Locks.ClientInstanceEstateLock.EnterWriteLock();
                                    Client.Estate.RemoveAllowedUser(targetUUID, allEstates);
                                    Locks.ClientInstanceEstateLock.ExitWriteLock();
                                    break;

                                default:
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.UNKNOWN_ESTATE_LIST_ACTION);
                            }
                            break;

                        case Enumerations.Type.MANAGER:
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                            corradeCommandParameters.Message)), out targetUUID) &&
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
                                    ref targetUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                            switch (
                                Reflection.GetEnumValueFromName<Enumerations.Action>(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                            corradeCommandParameters.Message))
                                ))
                            {
                                case Enumerations.Action.ADD:
                                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client))
                                    {
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
                                            !EstateListReceivedAlarm.Signal.WaitOne((int)
                                                corradeConfiguration.ServicesTimeout, false))
                                        {
                                            Client.Estate.EstateManagersReply -= EstateManagersReplyEventHandler;
                                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.TIMEOUT_RETRIEVING_ESTATE_LIST);
                                        }
                                        Client.Estate.EstateManagersReply -= EstateManagersReplyEventHandler;
                                        Locks.ClientInstanceEstateLock.ExitWriteLock();
                                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                                        if (estateList.Count >=
                                            wasOpenMetaverse.Constants.ESTATE.MAXIMUM_MANAGER_LIST_LENGTH)
                                        {
                                            Locks.ClientInstanceNetworkLock.ExitReadLock();
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.MAXIMUM_MANAGER_LIST_LENGTH_REACHED);
                                        }
                                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                                    }
                                    Locks.ClientInstanceEstateLock.EnterWriteLock();
                                    Client.Estate.AddEstateManager(targetUUID, allEstates);
                                    Locks.ClientInstanceEstateLock.ExitWriteLock();
                                    break;

                                case Enumerations.Action.REMOVE:
                                    Locks.ClientInstanceEstateLock.EnterWriteLock();
                                    Client.Estate.RemoveEstateManager(targetUUID, allEstates);
                                    Locks.ClientInstanceEstateLock.ExitWriteLock();
                                    break;

                                default:
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.UNKNOWN_ESTATE_LIST_ACTION);
                            }
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ESTATE_LIST);
                    }
                };
        }
    }
}