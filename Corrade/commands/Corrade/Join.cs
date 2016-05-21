///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> join =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    UUID groupUUID;
                    string target = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TARGET)),
                            corradeCommandParameters.Message));
                    switch (string.IsNullOrEmpty(target))
                    {
                        case false:
                            if (!UUID.TryParse(target, out groupUUID) &&
                                !Resolvers.GroupNameToUUID(Client, target, corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout,
                                    new Time.DecayingAlarm(corradeConfiguration.DataDecayType), ref groupUUID))
                                throw new ScriptException(ScriptError.GROUP_NOT_FOUND);
                            break;
                        default:
                            groupUUID = corradeCommandParameters.Group.UUID;
                            break;
                    }
                    IEnumerable<UUID> currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                    }
                    HashSet<UUID> groups = new HashSet<UUID>(currentGroups);
                    if (groups.Contains(groupUUID))
                    {
                        throw new ScriptException(ScriptError.ALREADY_IN_GROUP);
                    }
                    Group targetGroup = new Group();
                    if (
                        !Services.RequestGroup(Client, groupUUID,
                            corradeConfiguration.ServicesTimeout,
                            ref targetGroup))
                    {
                        throw new ScriptException(ScriptError.GROUP_NOT_FOUND);
                    }
                    if (!targetGroup.OpenEnrollment)
                    {
                        throw new ScriptException(ScriptError.GROUP_NOT_OPEN);
                    }
                    lock (Locks.ClientInstanceNetworkLock)
                    {
                        if (!Client.Network.MaxAgentGroups.Equals(-1))
                        {
                            if (groups.Count >= Client.Network.MaxAgentGroups)
                            {
                                throw new ScriptException(ScriptError.MAXIMUM_NUMBER_OF_GROUPS_REACHED);
                            }
                        }
                    }
                    ManualResetEvent GroupJoinedReplyEvent = new ManualResetEvent(false);
                    EventHandler<GroupOperationEventArgs> GroupOperationEventHandler =
                        (sender, args) => GroupJoinedReplyEvent.Set();
                    lock (Locks.ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupJoinedReply += GroupOperationEventHandler;
                        Client.Groups.RequestJoinGroup(groupUUID);
                        if (!GroupJoinedReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Groups.GroupJoinedReply -= GroupOperationEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_JOINING_GROUP);
                        }
                        Client.Groups.GroupJoinedReply -= GroupOperationEventHandler;
                    }
                    currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                    }
                    groups = new HashSet<UUID>(currentGroups);
                    if (!groups.Contains(groupUUID))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_JOIN_GROUP);
                    }
                };
        }
    }
}