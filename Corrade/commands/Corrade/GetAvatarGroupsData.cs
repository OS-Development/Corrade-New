///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getavatargroupsdata =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    UUID agentUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT)),
                                corradeCommandParameters.Message)),
                            out agentUUID) && !Resolvers.AgentNameToUUID(Client,
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME)),
                                        corradeCommandParameters.Message)),
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME)),
                                        corradeCommandParameters.Message)),
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType),
                                ref agentUUID))
                    {
                        throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                    }
                    var LockObject = new object();
                    var avatarGroups = new HashSet<AvatarGroup>();
                    var AvatarGroupsReceivedEvent =
                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType);
                    EventHandler<AvatarGroupsReplyEventArgs> AvatarGroupsReplyEventHandler = (sender, args) =>
                    {
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
                    lock (Locks.ClientInstanceAvatarsLock)
                    {
                        Client.Avatars.AvatarGroupsReply += AvatarGroupsReplyEventHandler;
                        Client.Avatars.RequestAvatarProperties(agentUUID);
                        if (
                            !AvatarGroupsReceivedEvent.Signal.WaitOne((int) corradeConfiguration.ServicesTimeout,
                                false))
                        {
                            Client.Avatars.AvatarGroupsReply -= AvatarGroupsReplyEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_AVATAR_DATA);
                        }
                        Client.Avatars.AvatarGroupsReply -= AvatarGroupsReplyEventHandler;
                    }
                    var data = new List<string>();
                    lock (LockObject)
                    {
                        avatarGroups.AsParallel().ForAll(o =>
                        {
                            data.AddRange(GetStructuredData(o,
                                wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                    corradeCommandParameters.Message))));
                        });
                    }
                    if (data.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                    }
                };
        }
    }
}