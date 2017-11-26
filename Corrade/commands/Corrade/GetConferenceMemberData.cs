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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getconferencememberdata =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Talk))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        UUID agentUUID;
                        if (
                            !UUID.TryParse(
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                    corradeCommandParameters.Message)),
                                out agentUUID) && !Resolvers.AgentNameToUUID(Client,
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME)),
                                        corradeCommandParameters.Message)),
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                        corradeCommandParameters.Message)),
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new DecayingAlarm(corradeConfiguration.DataDecayType),
                                ref agentUUID))
                            throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                        UUID sessionUUID;
                        // Get the session UUID
                        if (!UUID.TryParse(wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SESSION)),
                                corradeCommandParameters.Message)), out sessionUUID))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_SESSION_SPECIFIED);
                        List<ChatSessionMember> members;
                        if (!Client.Self.GroupChatSessions.TryGetValue(sessionUUID, out members))
                            throw new Command.ScriptException(Enumerations.ScriptError.SESSION_NOT_FOUND);
                        var chatSessionMember = members.FirstOrDefault(o => o.AvatarKey.Equals(agentUUID));

                        if (chatSessionMember.Equals(default(ChatSessionMember)))
                            throw new Command.ScriptException(Enumerations.ScriptError.CONFERENCE_MEMBER_NOT_FOUND);

                        var data =
                            chatSessionMember.GetStructuredData(
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))).ToList();

                        if (data.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(data));
                    };
        }
    }
}