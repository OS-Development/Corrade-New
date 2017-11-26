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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> batcheject =
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
                    if (!new HashSet<UUID>(currentGroups).Contains(groupUUID))
                        throw new Command.ScriptException(Enumerations.ScriptError.NOT_IN_GROUP);
                    if (
                        !Services.HasGroupPowers(Client, Client.Self.AgentID, groupUUID,
                            GroupPowers.Eject,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType)) ||
                        !Services.HasGroupPowers(Client, Client.Self.AgentID, groupUUID,
                            GroupPowers.RemoveMember,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    // Get the group members.
                    Dictionary<UUID, GroupMember> groupMembers = null;
                    var groupMembersReceivedEvent = new ManualResetEventSlim(false);
                    var groupMembersRequestUUID = UUID.Zero;
                    EventHandler<GroupMembersReplyEventArgs> HandleGroupMembersReplyDelegate = (sender, args) =>
                    {
                        if (!groupMembersRequestUUID.Equals(args.RequestID)) return;
                        groupMembers = args.Members;
                        groupMembersReceivedEvent.Set();
                    };
                    Client.Groups.GroupMembersReply += HandleGroupMembersReplyDelegate;
                    groupMembersRequestUUID = Client.Groups.RequestGroupMembers(groupUUID);
                    if (!groupMembersReceivedEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_GROUP_MEMBERS);
                    }
                    Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;

                    var targetGroup = new Group();
                    if (
                        !Services.RequestGroup(Client, groupUUID,
                            corradeConfiguration.ServicesTimeout,
                            ref targetGroup))
                        throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                    // Get roles members.
                    List<KeyValuePair<UUID, UUID>> groupRolesMembers = null;
                    var GroupRoleMembersReplyEvent = new ManualResetEventSlim(false);
                    var groupRolesMembersRequestUUID = UUID.Zero;
                    EventHandler<GroupRolesMembersReplyEventArgs> GroupRoleMembersEventHandler = (sender, args) =>
                    {
                        if (!groupRolesMembersRequestUUID.Equals(args.RequestID)) return;
                        groupRolesMembers = args.RolesMembers;
                        GroupRoleMembersReplyEvent.Set();
                    };
                    Client.Groups.GroupRoleMembersReply += GroupRoleMembersEventHandler;
                    groupRolesMembersRequestUUID = Client.Groups.RequestGroupRolesMembers(groupUUID);
                    if (!GroupRoleMembersReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_GROUP_ROLE_MEMBERS);
                    }
                    Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;

                    var demote = true;
                    bool.TryParse(wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DEMOTE)),
                        corradeCommandParameters.Message)), out demote);

                    var data = new HashSet<string>();
                    var LockObject = new object();
                    CSV.ToEnumerable(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AVATARS)),
                                    corradeCommandParameters.Message)))
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
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
                                    // Add all the unrecognized agents to the returned list.
                                    lock (LockObject)
                                    {
                                        if (!data.Contains(o))
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
                                        if (!data.Contains(o))
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
                                        if (!data.Contains(o))
                                            data.Add(o);
                                    }
                                    return;
                            }
                            // If demote is false and the group member belongs to any other roles
                            // other than the everyone role then we cannot proceed.
                            switch (
                                !groupRolesMembers.AsParallel()
                                    .Where(p => p.Value.Equals(agentUUID))
                                    .All(p => p.Key.Equals(UUID.Zero)))
                            {
                                case true:
                                    if (!demote) // need demote to eject member.
                                        lock (LockObject)
                                        {
                                            if (!data.Contains(o))
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
                                        Client.Groups.RemoveFromRole(groupUUID, p.Key,
                                            agentUUID);
                                    });
                            // And eject them.
                            var GroupEjectEvent = new ManualResetEventSlim(false);
                            var succeeded = false;
                            EventHandler<GroupOperationEventArgs> GroupOperationEventHandler =
                                (sender, args) =>
                                {
                                    if (!args.GroupID.Equals(groupUUID))
                                        return;
                                    succeeded = args.Success;
                                    GroupEjectEvent.Set();
                                };
                            Locks.ClientInstanceGroupsLock.EnterWriteLock();
                            Client.Groups.GroupMemberEjected += GroupOperationEventHandler;
                            Client.Groups.EjectUser(groupUUID, agentUUID);
                            GroupEjectEvent.Wait((int) corradeConfiguration.ServicesTimeout);
                            Client.Groups.GroupMemberEjected -= GroupOperationEventHandler;
                            Locks.ClientInstanceGroupsLock.ExitWriteLock();
                            // If the eject was not successful, add them to the output.
                            switch (succeeded)
                            {
                                case false:
                                    lock (LockObject)
                                    {
                                        if (!data.Contains(o))
                                            data.Add(o);
                                    }
                                    break;
                            }
                        });
                    if (data.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                };
        }
    }
}