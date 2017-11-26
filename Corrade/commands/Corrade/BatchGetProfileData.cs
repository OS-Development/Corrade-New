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
                batchgetprofiledata =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Interact))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        var fields =
                            wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(fields))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_DATA_PROVIDED);
                        var csv = new List<string>();
                        var error = new HashSet<string>();
                        foreach (var o in CSV.ToEnumerable(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AVATARS)),
                                    corradeCommandParameters.Message))))
                        {
                            UUID agentUUID;
                            if (!UUID.TryParse(o, out agentUUID))
                            {
                                var fullName = new List<string>(wasOpenMetaverse.Helpers.GetAvatarNames(o));
                                if (!fullName.Any() ||
                                    !Resolvers.AgentNameToUUID(Client, fullName.First(), fullName.Last(),
                                        corradeConfiguration.ServicesTimeout,
                                        corradeConfiguration.DataTimeout,
                                        new DecayingAlarm(corradeConfiguration.DataDecayType), ref agentUUID))
                                {
                                    // Add all the unrecognized agents to the error list.
                                    if (!error.Contains(o))
                                        error.Add(o);
                                    continue;
                                }
                            }

                            var ProfileDataReceivedAlarm = new DecayingAlarm(corradeConfiguration.DataDecayType);
                            var properties = new Avatar.AvatarProperties();
                            var interests = new Avatar.Interests();
                            var groups = new List<AvatarGroup>();
                            AvatarPicksReplyEventArgs picks = null;
                            AvatarClassifiedReplyEventArgs classifieds = null;
                            var LockObject = new object();
                            EventHandler<AvatarInterestsReplyEventArgs> AvatarInterestsReplyEventHandler =
                                (sender, args) =>
                                {
                                    if (!args.AvatarID.Equals(agentUUID))
                                        return;
                                    ProfileDataReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                                    interests = args.Interests;
                                };
                            EventHandler<AvatarPropertiesReplyEventArgs> AvatarPropertiesReplyEventHandler =
                                (sender, args) =>
                                {
                                    if (!args.AvatarID.Equals(agentUUID))
                                        return;
                                    ProfileDataReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                                    properties = args.Properties;
                                };
                            EventHandler<AvatarGroupsReplyEventArgs> AvatarGroupsReplyEventHandler = (sender, args) =>
                            {
                                if (!args.AvatarID.Equals(agentUUID))
                                    return;
                                ProfileDataReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                                lock (LockObject)
                                {
                                    groups.AddRange(args.Groups);
                                }
                            };
                            EventHandler<AvatarPicksReplyEventArgs> AvatarPicksReplyEventHandler =
                                (sender, args) =>
                                {
                                    if (!args.AvatarID.Equals(agentUUID))
                                        return;
                                    ProfileDataReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                                    picks = args;
                                };
                            EventHandler<AvatarClassifiedReplyEventArgs> AvatarClassifiedReplyEventHandler =
                                (sender, args) =>
                                {
                                    if (!args.AvatarID.Equals(agentUUID))
                                        return;
                                    ProfileDataReceivedAlarm.Alarm(corradeConfiguration.DataTimeout);
                                    classifieds = args;
                                };
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
                                // Add all the unrecognized agents to the error list.
                                if (!error.Contains(o))
                                {
                                    error.Add(o);
                                    continue;
                                }
                            }
                            Client.Avatars.AvatarInterestsReply -= AvatarInterestsReplyEventHandler;
                            Client.Avatars.AvatarPropertiesReply -= AvatarPropertiesReplyEventHandler;
                            Client.Avatars.AvatarGroupsReply -= AvatarGroupsReplyEventHandler;
                            Client.Avatars.AvatarPicksReply -= AvatarPicksReplyEventHandler;
                            Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedReplyEventHandler;
                            Locks.ClientInstanceAvatarsLock.ExitReadLock();

                            csv.AddRange(properties.GetStructuredData(fields));
                            csv.AddRange(interests.GetStructuredData(fields));
                            csv.AddRange(groups.GetStructuredData(fields));
                            if (picks != null)
                                csv.AddRange(picks.GetStructuredData(fields));
                            if (classifieds != null)
                                csv.AddRange(classifieds.GetStructuredData(fields));

                            if (error.Any())
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.ERROR),
                                    CSV.FromEnumerable(error));

                            if (csv.Any())
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                    CSV.FromEnumerable(csv));
                        }
                    };
        }
    }
}