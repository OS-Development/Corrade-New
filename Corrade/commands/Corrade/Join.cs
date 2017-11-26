///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> join =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Group))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    UUID groupUUID;
                    var target = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                            corradeCommandParameters.Message));
                    switch (string.IsNullOrEmpty(target))
                    {
                        case false:
                            if (!UUID.TryParse(target, out groupUUID) &&
                                !Resolvers.GroupNameToUUID(Client, target, corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout,
                                    new DecayingAlarm(corradeConfiguration.DataDecayType), ref groupUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                            break;

                        default:
                            groupUUID = corradeCommandParameters.Group.UUID;
                            break;
                    }
                    var currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                    var groups = new HashSet<UUID>(currentGroups);
                    if (groups.Contains(groupUUID))
                        throw new Command.ScriptException(Enumerations.ScriptError.ALREADY_IN_GROUP);
                    var targetGroup = new Group();
                    if (
                        !Services.RequestGroup(Client, groupUUID,
                            corradeConfiguration.ServicesTimeout,
                            ref targetGroup))
                        throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                    if (!targetGroup.OpenEnrollment)
                        throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_OPEN);
                    Locks.ClientInstanceNetworkLock.EnterReadLock();
                    if (!Client.Network.MaxAgentGroups.Equals(-1) && groups.Count >= Client.Network.MaxAgentGroups)
                    {
                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.MAXIMUM_NUMBER_OF_GROUPS_REACHED);
                    }
                    Locks.ClientInstanceNetworkLock.ExitReadLock();
                    var GroupJoinedReplyEvent = new ManualResetEventSlim(false);
                    EventHandler<GroupOperationEventArgs> GroupOperationEventHandler =
                        (sender, args) =>
                        {
                            if (!args.GroupID.Equals(groupUUID))
                                return;
                            GroupJoinedReplyEvent.Set();
                        };
                    Locks.ClientInstanceGroupsLock.EnterWriteLock();
                    Client.Groups.GroupJoinedReply += GroupOperationEventHandler;
                    Client.Groups.RequestJoinGroup(groupUUID);
                    if (!GroupJoinedReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Groups.GroupJoinedReply -= GroupOperationEventHandler;
                        Locks.ClientInstanceGroupsLock.ExitWriteLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_JOINING_GROUP);
                    }
                    Client.Groups.GroupJoinedReply -= GroupOperationEventHandler;
                    Locks.ClientInstanceGroupsLock.ExitWriteLock();
                    currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                    groups = new HashSet<UUID>(currentGroups);
                    if (!groups.Contains(groupUUID))
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_JOIN_GROUP);
                };
        }
    }
}