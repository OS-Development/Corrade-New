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
            public static Action<Group, string, Dictionary<string, string>> moderate =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    if (
                        !HasGroupPowers(Client.Self.AgentID, commandGroup.UUID, GroupPowers.ModerateChat,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    UUID agentUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT)), message)),
                            out agentUUID) && !AgentNameToUUID(
                                wasInput(
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME)),
                                        message)),
                                wasInput(
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME)),
                                        message)),
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                ref agentUUID))
                    {
                        throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                    }
                    IEnumerable<UUID> currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !GetCurrentGroups(corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                    }
                    if (!currentGroups.ToList().Any(o => o.Equals(commandGroup.UUID)))
                    {
                        throw new ScriptException(ScriptError.NOT_IN_GROUP);
                    }
                    bool silence;
                    if (
                        !bool.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.SILENCE)),
                                    message)),
                            out silence))
                    {
                        silence = false;
                    }
                    Type type =
                        wasGetEnumValueFromDescription<Type>(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)), message))
                                .ToLowerInvariant());
                    switch (type)
                    {
                        case Type.TEXT:
                        case Type.VOICE:
                            Client.Self.ModerateChatSessions(commandGroup.UUID, agentUUID,
                                wasGetDescriptionFromEnumValue(type),
                                silence);
                            break;
                        default:
                            throw new ScriptException(ScriptError.TYPE_CAN_BE_VOICE_OR_TEXT);
                    }
                };
        }
    }
}