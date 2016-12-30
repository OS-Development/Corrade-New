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
                replytofriendshiprequest
                    =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Friendship))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        }
                        var action = Reflection.GetEnumValueFromName<Enumerations.Action>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                        corradeCommandParameters.Message)));
                        var agentUUID = UUID.Zero;
                        var session = UUID.Zero;
                        switch (action)
                        {
                            case Enumerations.Action.ACCEPT:
                            case Enumerations.Action.DECLINE:
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
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                                }
                                lock (Locks.ClientInstanceFriendsLock)
                                {
                                    if (!Client.Friends.FriendRequests.TryGetValue(agentUUID, out session))
                                    {
                                        throw new Command.ScriptException(Enumerations.ScriptError.FRIENDSHIP_OFFER_NOT_FOUND);
                                    }
                                }
                                break;
                        }
                        switch (action)
                        {
                            case Enumerations.Action.ACCEPT:
                                lock (Locks.ClientInstanceFriendsLock)
                                {
                                    Client.Friends.AcceptFriendship(agentUUID, session);
                                }
                                break;
                            case Enumerations.Action.DECLINE:
                                lock (Locks.ClientInstanceFriendsLock)
                                {
                                    Client.Friends.DeclineFriendship(agentUUID, session);
                                }
                                break;
                            default:
                                throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                        }
                    };
        }
    }
}