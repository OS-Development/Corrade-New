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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getprofilesdata
                =
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
                    var region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                corradeCommandParameters.Message));
                    var simulators = new List<Simulator>();
                    Locks.ClientInstanceNetworkLock.EnterReadLock();
                    var simulator = Client.Network.Simulators.AsParallel().FirstOrDefault(
                        o => string.Equals(o.Name, region, StringComparison.OrdinalIgnoreCase));
                    switch (simulator != null && !simulators.Equals(default(Simulator)))
                    {
                        case true:
                            simulators.Add(simulator);
                            break;

                        default:
                            simulators.AddRange(Client.Network.Simulators);
                            break;
                    }
                    Locks.ClientInstanceNetworkLock.ExitReadLock();
                    var csv = new List<string>();
                    foreach (var agentUUID in simulators.SelectMany(o => o.AvatarPositions.Copy().Keys))
                    {
                        var ProfileDataReceivedAlarm = new DecayingAlarm(corradeConfiguration.DataDecayType);
                        var properties = new Avatar.AvatarProperties();
                        var interests = new Avatar.Interests();
                        var groups = new List<AvatarGroup>();
                        AvatarPicksReplyEventArgs picks = null;
                        AvatarClassifiedReplyEventArgs classifieds = null;
                        var LockObject = new object();
                        EventHandler<AvatarInterestsReplyEventArgs> AvatarInterestsReplyEventHandler = (sender, args) =>
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
                            Locks.ClientInstanceAvatarsLock.ExitReadLock();
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_AVATAR_DATA);
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
                    }

                    if (csv.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                };
        }
    }
}