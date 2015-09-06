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
            public static Action<Group, string, Dictionary<string, string>> setestatelist =
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
                    bool allEstates;
                    if (
                        !bool.TryParse(
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ALL)),
                                message)),
                            out allEstates))
                    {
                        allEstates = false;
                    }
                    List<UUID> estateList = new List<UUID>();
                    wasAdaptiveAlarm EstateListReceivedAlarm =
                        new wasAdaptiveAlarm(corradeConfiguration.DataDecayType);
                    UUID targetUUID;
                    switch (
                        wasGetEnumValueFromDescription<Type>(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)), message))
                                .ToLowerInvariant()))
                    {
                        case Type.BAN:
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT)),
                                            message)), out targetUUID) && !AgentNameToUUID(
                                                wasInput(
                                                    wasKeyValueGet(
                                                        wasOutput(
                                                            wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME)),
                                                        message)),
                                                wasInput(
                                                    wasKeyValueGet(
                                                        wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME)),
                                                        message)),
                                                corradeConfiguration.ServicesTimeout,
                                                corradeConfiguration.DataTimeout,
                                                ref targetUUID))
                            {
                                throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                            }
                            switch (
                                wasGetEnumValueFromDescription<Action>(
                                    wasInput(
                                        wasKeyValueGet(
                                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)), message))
                                        .ToLowerInvariant()))
                            {
                                case Action.ADD:
                                    // if this is SecondLife check that we would not exeed the maximum amount of bans
                                    if (IsSecondLife())
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
                                        lock (ClientInstanceEstateLock)
                                        {
                                            Client.Estate.EstateBansReply += EstateBansReplyEventHandler;
                                            Client.Estate.RequestInfo();
                                            if (
                                                !EstateListReceivedAlarm.Signal.WaitOne((int)
                                                    corradeConfiguration.ServicesTimeout,
                                                    false))
                                            {
                                                Client.Estate.EstateBansReply -= EstateBansReplyEventHandler;
                                                throw new Exception(
                                                    wasGetDescriptionFromEnumValue(
                                                        ScriptError.TIMEOUT_RETRIEVING_ESTATE_LIST));
                                            }
                                            Client.Estate.EstateBansReply -= EstateBansReplyEventHandler;
                                        }
                                        lock (ClientInstanceNetworkLock)
                                        {
                                            if (estateList.Count >= LINDEN_CONSTANTS.ESTATE.MAXIMUM_BAN_LIST_LENGTH)
                                            {
                                                throw new Exception(
                                                    wasGetDescriptionFromEnumValue(
                                                        ScriptError.MAXIMUM_BAN_LIST_LENGTH_REACHED));
                                            }
                                        }
                                    }
                                    Client.Estate.BanUser(targetUUID, allEstates);
                                    break;
                                case Action.REMOVE:
                                    Client.Estate.UnbanUser(targetUUID, allEstates);
                                    break;
                                default:
                                    throw new ScriptException(ScriptError.UNKNOWN_ESTATE_LIST_ACTION);
                            }
                            break;
                        case Type.GROUP:
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TARGET)),
                                            message)),
                                    out targetUUID) && !GroupNameToUUID(
                                        wasInput(
                                            wasKeyValueGet(
                                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TARGET)),
                                                message)),
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        ref targetUUID))
                            {
                                throw new ScriptException(ScriptError.GROUP_NOT_FOUND);
                            }
                            switch (
                                wasGetEnumValueFromDescription<Action>(
                                    wasInput(
                                        wasKeyValueGet(
                                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)), message))
                                        .ToLowerInvariant()))
                            {
                                case Action.ADD:
                                    if (IsSecondLife())
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
                                        lock (ClientInstanceEstateLock)
                                        {
                                            Client.Estate.EstateGroupsReply += EstateGroupsReplyEvenHandler;
                                            Client.Estate.RequestInfo();
                                            if (
                                                !EstateListReceivedAlarm.Signal.WaitOne((int)
                                                    corradeConfiguration.ServicesTimeout, false))
                                            {
                                                Client.Estate.EstateGroupsReply -= EstateGroupsReplyEvenHandler;
                                                throw new Exception(
                                                    wasGetDescriptionFromEnumValue(
                                                        ScriptError.TIMEOUT_RETRIEVING_ESTATE_LIST));
                                            }
                                            Client.Estate.EstateGroupsReply -= EstateGroupsReplyEvenHandler;
                                        }
                                        lock (ClientInstanceNetworkLock)
                                        {
                                            if (estateList.Count >=
                                                LINDEN_CONSTANTS.ESTATE.MAXIMUM_GROUP_LIST_LENGTH)
                                            {
                                                throw new Exception(
                                                    wasGetDescriptionFromEnumValue(
                                                        ScriptError.MAXIMUM_GROUP_LIST_LENGTH_REACHED));
                                            }
                                        }
                                    }
                                    Client.Estate.AddAllowedGroup(targetUUID, allEstates);
                                    break;
                                case Action.REMOVE:
                                    Client.Estate.RemoveAllowedGroup(targetUUID, allEstates);
                                    break;
                                default:
                                    throw new ScriptException(ScriptError.UNKNOWN_ESTATE_LIST_ACTION);
                            }
                            break;
                        case Type.USER:
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT)),
                                            message)), out targetUUID) && !AgentNameToUUID(
                                                wasInput(
                                                    wasKeyValueGet(
                                                        wasOutput(
                                                            wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME)),
                                                        message)),
                                                wasInput(
                                                    wasKeyValueGet(
                                                        wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME)),
                                                        message)),
                                                corradeConfiguration.ServicesTimeout,
                                                corradeConfiguration.DataTimeout,
                                                ref targetUUID))
                            {
                                throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                            }
                            switch (
                                wasGetEnumValueFromDescription<Action>(
                                    wasInput(
                                        wasKeyValueGet(
                                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)), message))
                                        .ToLowerInvariant()))
                            {
                                case Action.ADD:
                                    if (IsSecondLife())
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
                                        lock (ClientInstanceEstateLock)
                                        {
                                            Client.Estate.EstateUsersReply += EstateUsersReplyEventHandler;
                                            Client.Estate.RequestInfo();
                                            if (
                                                !EstateListReceivedAlarm.Signal.WaitOne((int)
                                                    corradeConfiguration.ServicesTimeout, false))
                                            {
                                                Client.Estate.EstateUsersReply -= EstateUsersReplyEventHandler;
                                                throw new Exception(
                                                    wasGetDescriptionFromEnumValue(
                                                        ScriptError.TIMEOUT_RETRIEVING_ESTATE_LIST));
                                            }
                                            Client.Estate.EstateUsersReply -= EstateUsersReplyEventHandler;
                                        }
                                        lock (ClientInstanceNetworkLock)
                                        {
                                            if (estateList.Count >= LINDEN_CONSTANTS.ESTATE.MAXIMUM_USER_LIST_LENGTH)
                                            {
                                                throw new Exception(
                                                    wasGetDescriptionFromEnumValue(
                                                        ScriptError.MAXIMUM_USER_LIST_LENGTH_REACHED));
                                            }
                                        }
                                    }
                                    Client.Estate.AddAllowedUser(targetUUID, allEstates);
                                    break;
                                case Action.REMOVE:
                                    Client.Estate.RemoveAllowedUser(targetUUID, allEstates);
                                    break;
                                default:
                                    throw new ScriptException(ScriptError.UNKNOWN_ESTATE_LIST_ACTION);
                            }
                            break;
                        case Type.MANAGER:
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT)),
                                            message)), out targetUUID) && !AgentNameToUUID(
                                                wasInput(
                                                    wasKeyValueGet(
                                                        wasOutput(
                                                            wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME)),
                                                        message)),
                                                wasInput(
                                                    wasKeyValueGet(
                                                        wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME)),
                                                        message)),
                                                corradeConfiguration.ServicesTimeout,
                                                corradeConfiguration.DataTimeout,
                                                ref targetUUID))
                            {
                                throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                            }
                            switch (
                                wasGetEnumValueFromDescription<Action>(
                                    wasInput(
                                        wasKeyValueGet(
                                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)), message))
                                        .ToLowerInvariant()))
                            {
                                case Action.ADD:
                                    if (IsSecondLife())
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
                                        lock (ClientInstanceEstateLock)
                                        {
                                            Client.Estate.EstateManagersReply += EstateManagersReplyEventHandler;
                                            Client.Estate.RequestInfo();
                                            if (
                                                !EstateListReceivedAlarm.Signal.WaitOne((int)
                                                    corradeConfiguration.ServicesTimeout, false))
                                            {
                                                Client.Estate.EstateManagersReply -= EstateManagersReplyEventHandler;
                                                throw new Exception(
                                                    wasGetDescriptionFromEnumValue(
                                                        ScriptError.TIMEOUT_RETRIEVING_ESTATE_LIST));
                                            }
                                            Client.Estate.EstateManagersReply -= EstateManagersReplyEventHandler;
                                        }
                                        lock (ClientInstanceNetworkLock)
                                        {
                                            if (estateList.Count >=
                                                LINDEN_CONSTANTS.ESTATE.MAXIMUM_MANAGER_LIST_LENGTH)
                                            {
                                                throw new Exception(
                                                    wasGetDescriptionFromEnumValue(
                                                        ScriptError.MAXIMUM_MANAGER_LIST_LENGTH_REACHED));
                                            }
                                        }
                                    }
                                    Client.Estate.AddEstateManager(targetUUID, allEstates);
                                    break;
                                case Action.REMOVE:
                                    Client.Estate.RemoveEstateManager(targetUUID, allEstates);
                                    break;
                                default:
                                    throw new ScriptException(ScriptError.UNKNOWN_ESTATE_LIST_ACTION);
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ESTATE_LIST);
                    }
                };
        }
    }
}