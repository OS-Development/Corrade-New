///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getavatardata =
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
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                            out range))
                        range = corradeConfiguration.Range;
                    var avatar = Services.GetAvatars(Client, range)
                        .AsParallel()
                        .FirstOrDefault(o => o.ID.Equals(agentUUID));
                    if (avatar == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.AVATAR_NOT_IN_RANGE);
                    var ProfileDataReceivedAlarm =
                        new DecayingAlarm(corradeConfiguration.DataDecayType);
                    var LockObject = new object();
                    EventHandler<AvatarInterestsReplyEventArgs> AvatarInterestsReplyEventHandler = (sender, args) =>
                    {
                        if (!args.AvatarID.Equals(agentUUID))
                            return;

                        ProfileDataReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                        avatar.ProfileInterests = args.Interests;
                    };
                    EventHandler<AvatarPropertiesReplyEventArgs> AvatarPropertiesReplyEventHandler =
                        (sender, args) =>
                        {
                            if (!args.AvatarID.Equals(agentUUID))
                                return;

                            ProfileDataReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                            avatar.ProfileProperties = args.Properties;
                        };
                    EventHandler<AvatarGroupsReplyEventArgs> AvatarGroupsReplyEventHandler = (sender, args) =>
                    {
                        if (!args.AvatarID.Equals(agentUUID))
                            return;

                        ProfileDataReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                        lock (LockObject)
                        {
                            avatar.Groups.AddRange(args.Groups.Select(o => o.GroupID));
                        }
                    };
                    EventHandler<AvatarPicksReplyEventArgs> AvatarPicksReplyEventHandler =
                        (sender, args) => ProfileDataReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                    EventHandler<AvatarClassifiedReplyEventArgs> AvatarClassifiedReplyEventHandler =
                        (sender, args) => ProfileDataReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                    Locks.ClientInstanceAvatarsLock.EnterReadLock();
                    Client.Avatars.AvatarInterestsReply += AvatarInterestsReplyEventHandler;
                    Client.Avatars.AvatarPropertiesReply += AvatarPropertiesReplyEventHandler;
                    Client.Avatars.AvatarGroupsReply += AvatarGroupsReplyEventHandler;
                    Client.Avatars.AvatarPicksReply += AvatarPicksReplyEventHandler;
                    Client.Avatars.AvatarClassifiedReply += AvatarClassifiedReplyEventHandler;
                    Client.Avatars.RequestAvatarProperties(agentUUID);
                    Client.Avatars.RequestAvatarPicks(agentUUID);
                    Client.Avatars.RequestAvatarClassified(agentUUID);
                    if (
                        !ProfileDataReceivedAlarm.Signal.WaitOne((int) corradeConfiguration.ServicesTimeout,
                            false))
                    {
                        Client.Avatars.AvatarInterestsReply -= AvatarInterestsReplyEventHandler;
                        Client.Avatars.AvatarPropertiesReply -= AvatarPropertiesReplyEventHandler;
                        Client.Avatars.AvatarGroupsReply -= AvatarGroupsReplyEventHandler;
                        Client.Avatars.AvatarPicksReply -= AvatarPicksReplyEventHandler;
                        Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedReplyEventHandler;
                        Locks.ClientInstanceAvatarsLock.ExitReadLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_AVATAR_DATA);
                    }
                    Client.Avatars.AvatarInterestsReply -= AvatarInterestsReplyEventHandler;
                    Client.Avatars.AvatarPropertiesReply -= AvatarPropertiesReplyEventHandler;
                    Client.Avatars.AvatarGroupsReply -= AvatarGroupsReplyEventHandler;
                    Client.Avatars.AvatarPicksReply -= AvatarPicksReplyEventHandler;
                    Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedReplyEventHandler;
                    Locks.ClientInstanceAvatarsLock.ExitReadLock();
                    var data =
                        avatar.GetStructuredData(
                            wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                corradeCommandParameters.Message))).ToList();
                    if (data.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                };
        }
    }
}