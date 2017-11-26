///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> offerfriendship
                =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Friendship))
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
                    Locks.ClientInstanceFriendsLock.EnterReadLock();
                    if (Client.Friends.FriendList.ContainsKey(agentUUID))
                    {
                        Locks.ClientInstanceFriendsLock.ExitReadLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.AGENT_ALREADY_FRIEND);
                    }
                    Locks.ClientInstanceFriendsLock.ExitReadLock();
                    Locks.ClientInstanceFriendsLock.EnterWriteLock();
                    Client.Friends.OfferFriendship(agentUUID,
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MESSAGE)),
                                corradeCommandParameters.Message)));
                    Locks.ClientInstanceFriendsLock.ExitWriteLock();
                };
        }
    }
}