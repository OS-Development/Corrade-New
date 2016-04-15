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
using Helpers = wasOpenMetaverse.Helpers;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> batcheject =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    IEnumerable<UUID> currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                    }
                    if (!new HashSet<UUID>(currentGroups).Contains(corradeCommandParameters.Group.UUID))
                    {
                        throw new ScriptException(ScriptError.NOT_IN_GROUP);
                    }
                    if (
                        !Services.HasGroupPowers(Client, Client.Self.AgentID, corradeCommandParameters.Group.UUID,
                            GroupPowers.Eject,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new Time.DecayingAlarm(corradeConfiguration.DataDecayType)) ||
                        !Services.HasGroupPowers(Client, Client.Self.AgentID, corradeCommandParameters.Group.UUID,
                            GroupPowers.RemoveMember,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    // Get the group members.
                    Dictionary<UUID, GroupMember> groupMembers = null;
                    ManualResetEvent groupMembersReceivedEvent = new ManualResetEvent(false);
                    EventHandler<GroupMembersReplyEventArgs> HandleGroupMembersReplyDelegate = (sender, args) =>
                    {
                        groupMembers = args.Members;
                        groupMembersReceivedEvent.Set();
                    };
                    lock (Locks.ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupMembersReply += HandleGroupMembersReplyDelegate;
                        Client.Groups.RequestGroupMembers(corradeCommandParameters.Group.UUID);
                        if (!groupMembersReceivedEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_GROUP_MEMBERS);
                        }
                        Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                    }
                    Group targetGroup = new Group();
                    if (
                        !Services.RequestGroup(Client, corradeCommandParameters.Group.UUID,
                            corradeConfiguration.ServicesTimeout,
                            ref targetGroup))
                    {
                        throw new ScriptException(ScriptError.GROUP_NOT_FOUND);
                    }
                    // Get roles members.
                    List<KeyValuePair<UUID, UUID>> groupRolesMembers = null;
                    ManualResetEvent GroupRoleMembersReplyEvent = new ManualResetEvent(false);
                    EventHandler<GroupRolesMembersReplyEventArgs> GroupRoleMembersEventHandler = (sender, args) =>
                    {
                        groupRolesMembers = args.RolesMembers;
                        GroupRoleMembersReplyEvent.Set();
                    };
                    lock (Locks.ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupRoleMembersReply += GroupRoleMembersEventHandler;
                        Client.Groups.RequestGroupRolesMembers(corradeCommandParameters.Group.UUID);
                        if (!GroupRoleMembersReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_GROUP_ROLE_MEMBERS);
                        }
                        Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                    }
                    HashSet<string> data = new HashSet<string>();
                    object LockObject = new object();
                    CSV.ToEnumerable(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.AVATARS)),
                                corradeCommandParameters.Message)))
                        .ToArray()
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                        {
                            UUID agentUUID;
                            if (!UUID.TryParse(o, out agentUUID))
                            {
                                List<string> fullName = new List<string>(Helpers.GetAvatarNames(o));
                                if (
                                    !Resolvers.AgentNameToUUID(Client, fullName.First(), fullName.Last(),
                                        corradeConfiguration.ServicesTimeout,
                                        corradeConfiguration.DataTimeout,
                                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType), ref agentUUID))
                                {
                                    // Add all the unrecognized agents to the returned list.
                                    lock (LockObject)
                                    {
                                        data.Add(o);
                                    }
                                    return;
                                }
                            }
                            // Check if they are in the group.
                            switch (!groupMembers.AsParallel().Any(p => p.Value.ID.Equals(agentUUID)))
                            {
                                case true: // if they are not, add them to the returned list
                                    lock (LockObject)
                                    {
                                        data.Add(o);
                                    }
                                    return;
                            }
                            // The agent could be resolved and they are in the group.
                            // Check their status.
                            switch (
                                !groupRolesMembers.AsParallel()
                                    .Any(
                                        p =>
                                            p.Key.Equals(targetGroup.OwnerRole) && p.Value.Equals(agentUUID))
                                )
                            {
                                case false: // cannot demote owners
                                    lock (LockObject)
                                    {
                                        data.Add(o);
                                    }
                                    return;
                            }
                            // Demote them.
                            groupRolesMembers.AsParallel().Where(
                                p => p.Value.Equals(agentUUID))
                                .ForAll(
                                    p =>
                                    {
                                        Client.Groups.RemoveFromRole(corradeCommandParameters.Group.UUID, p.Key,
                                            agentUUID);
                                    });
                            // And eject them.
                            ManualResetEvent GroupEjectEvent = new ManualResetEvent(false);
                            bool succeeded = false;
                            EventHandler<GroupOperationEventArgs> GroupOperationEventHandler =
                                (sender, args) =>
                                {
                                    succeeded = args.Success;
                                    GroupEjectEvent.Set();
                                };
                            lock (Locks.ClientInstanceGroupsLock)
                            {
                                Client.Groups.GroupMemberEjected += GroupOperationEventHandler;
                                Client.Groups.EjectUser(corradeCommandParameters.Group.UUID, agentUUID);
                                GroupEjectEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false);
                                Client.Groups.GroupMemberEjected -= GroupOperationEventHandler;
                            }
                            // If the eject was not successful, add them to the output.
                            switch (succeeded)
                            {
                                case false:
                                    lock (LockObject)
                                    {
                                        data.Add(o);
                                    }
                                    break;
                            }
                        });
                    if (data.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                    }
                };
        }
    }
}