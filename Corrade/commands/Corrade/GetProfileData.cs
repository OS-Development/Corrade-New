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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getprofiledata =
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
                    var ProfileDataReceivedAlarm =
                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType);
                    var properties = new Avatar.AvatarProperties();
                    var interests = new Avatar.Interests();
                    var groups = new List<AvatarGroup>();
                    AvatarPicksReplyEventArgs picks = null;
                    AvatarClassifiedReplyEventArgs classifieds = null;
                    var LockObject = new object();
                    EventHandler<AvatarInterestsReplyEventArgs> AvatarInterestsReplyEventHandler = (sender, args) =>
                    {
                        ProfileDataReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                        interests = args.Interests;
                    };
                    EventHandler<AvatarPropertiesReplyEventArgs> AvatarPropertiesReplyEventHandler =
                        (sender, args) =>
                        {
                            ProfileDataReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                            properties = args.Properties;
                        };
                    EventHandler<AvatarGroupsReplyEventArgs> AvatarGroupsReplyEventHandler = (sender, args) =>
                    {
                        ProfileDataReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                        lock (LockObject)
                        {
                            groups.AddRange(args.Groups);
                        }
                    };
                    EventHandler<AvatarPicksReplyEventArgs> AvatarPicksReplyEventHandler =
                        (sender, args) =>
                        {
                            ProfileDataReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                            picks = args;
                        };
                    EventHandler<AvatarClassifiedReplyEventArgs> AvatarClassifiedReplyEventHandler =
                        (sender, args) =>
                        {
                            ProfileDataReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                            classifieds = args;
                        };
                    lock (Locks.ClientInstanceAvatarsLock)
                    {
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
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_AVATAR_DATA);
                        }
                        Client.Avatars.AvatarInterestsReply -= AvatarInterestsReplyEventHandler;
                        Client.Avatars.AvatarPropertiesReply -= AvatarPropertiesReplyEventHandler;
                        Client.Avatars.AvatarGroupsReply -= AvatarGroupsReplyEventHandler;
                        Client.Avatars.AvatarPicksReply -= AvatarPicksReplyEventHandler;
                        Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedReplyEventHandler;
                    }
                    var fields =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                            corradeCommandParameters.Message));
                    var csv = new List<string>();
                    csv.AddRange(GetStructuredData(properties, fields));
                    csv.AddRange(GetStructuredData(interests, fields));
                    csv.AddRange(GetStructuredData(groups, fields));
                    if (picks != null)
                    {
                        csv.AddRange(GetStructuredData(picks, fields));
                    }
                    if (classifieds != null)
                    {
                        csv.AddRange(GetStructuredData(classifieds, fields));
                    }
                    if (csv.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                    }
                };
        }
    }
}