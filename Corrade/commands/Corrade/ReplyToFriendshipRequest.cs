///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> replytofriendshiprequest =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Friendship))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
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
                    UUID session = UUID.Zero;
                    Client.Friends.FriendRequests.ForEach(o =>
                    {
                        if (o.Key.Equals(agentUUID))
                        {
                            session = o.Value;
                        }
                    });
                    if (session.Equals(UUID.Zero))
                    {
                        throw new ScriptException(ScriptError.NO_FRIENDSHIP_OFFER_FOUND);
                    }
                    switch (
                        wasGetEnumValueFromDescription<Action>(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)),
                                    message)).ToLowerInvariant()))
                    {
                        case Action.ACCEPT:
                            Client.Friends.AcceptFriendship(agentUUID, session);
                            break;
                        case Action.DECLINE:
                            Client.Friends.DeclineFriendship(agentUUID, session);
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}