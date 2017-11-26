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
                getavatargroupsdata =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Interact))
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
                        var LockObject = new object();
                        var avatarGroups = new HashSet<AvatarGroup>();
                        var AvatarGroupsReceivedEvent =
                            new DecayingAlarm(corradeConfiguration.DataDecayType);
                        EventHandler<AvatarGroupsReplyEventArgs> AvatarGroupsReplyEventHandler = (sender, args) =>
                        {
                            if (!args.AvatarID.Equals(agentUUID))
                                return;

                            AvatarGroupsReceivedEvent.Alarm(corradeConfiguration.DataTimeout);
                            args.Groups.AsParallel().ForAll(o =>
                            {
                                lock (LockObject)
                                {
                                    if (!avatarGroups.Contains(o))
                                        avatarGroups.Add(o);
                                }
                            });
                        };
                        Locks.ClientInstanceAvatarsLock.EnterReadLock();
                        Client.Avatars.AvatarGroupsReply += AvatarGroupsReplyEventHandler;
                        Client.Avatars.RequestAvatarProperties(agentUUID);
                        if (
                            !AvatarGroupsReceivedEvent.Signal.WaitOne((int) corradeConfiguration.ServicesTimeout,
                                false))
                        {
                            Client.Avatars.AvatarGroupsReply -= AvatarGroupsReplyEventHandler;
                            Locks.ClientInstanceAvatarsLock.ExitReadLock();
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_AVATAR_DATA);
                        }
                        Client.Avatars.AvatarGroupsReply -= AvatarGroupsReplyEventHandler;
                        Locks.ClientInstanceAvatarsLock.ExitReadLock();
                        var data = new List<string>();
                        lock (LockObject)
                        {
                            avatarGroups.AsParallel().ForAll(o =>
                            {
                                data.AddRange(o.GetStructuredData(wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))));
                            });
                        }
                        if (data.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(data));
                    };
        }
    }
}