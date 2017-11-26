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
using Parallel = System.Threading.Tasks.Parallel;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> batchinvite =
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
                            GroupPowers.Invite,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    // Get the roles to invite to.
                    var roleUUIDs = new HashSet<UUID>();
                    var rolesFound = true;
                    Parallel.ForEach(CSV.ToEnumerable(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ROLE)),
                                    corradeCommandParameters.Message)))
                        .AsParallel().Where(o => !string.IsNullOrEmpty(o)), (o, s) =>
                    {
                        UUID roleUUID;
                        if (!UUID.TryParse(o, out roleUUID) &&
                            !Resolvers.RoleNameToUUID(Client, o, groupUUID,
                                corradeConfiguration.ServicesTimeout, ref roleUUID))
                        {
                            rolesFound = false;
                            s.Break();
                        }
                        if (!roleUUIDs.Contains(roleUUID))
                            roleUUIDs.Add(roleUUID);
                    });
                    if (!rolesFound)
                        throw new Command.ScriptException(Enumerations.ScriptError.ROLE_NOT_FOUND);
                    // No roles specified, so assume everyone role.
                    if (!roleUUIDs.Any())
                        roleUUIDs.Add(UUID.Zero);
                    // If we are not inviting to the everyone role, then check whether we need the group power to
                    // assign just to the roles we are part of or whether we need the power to invite to any role.
                    if (!roleUUIDs.All(o => o.Equals(UUID.Zero)))
                    {
                        // get our current roles.
                        var selfRoles = new HashSet<UUID>();
                        var GroupRoleMembersReplyEvent = new ManualResetEventSlim(false);
                        var groupRolesMembersRequestUUID = UUID.Zero;
                        EventHandler<GroupRolesMembersReplyEventArgs> GroupRolesMembersEventHandler = (sender, args) =>
                        {
                            if (!groupRolesMembersRequestUUID.Equals(args.RequestID)) return;
                            selfRoles.UnionWith(
                                args.RolesMembers
                                    .AsParallel()
                                    .Where(o => o.Value.Equals(Client.Self.AgentID))
                                    .Select(o => o.Key));
                            GroupRoleMembersReplyEvent.Set();
                        };
                        Client.Groups.GroupRoleMembersReply += GroupRolesMembersEventHandler;
                        groupRolesMembersRequestUUID = Client.Groups.RequestGroupRolesMembers(groupUUID);
                        if (!GroupRoleMembersReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                        {
                            Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                            throw new Command.ScriptException(
                                Enumerations.ScriptError.TIMEOUT_GETING_GROUP_ROLES_MEMBERS);
                        }
                        Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                        if (!Services.HasGroupPowers(Client, Client.Self.AgentID,
                            groupUUID,
                            roleUUIDs.All(o => selfRoles.Contains(o))
                                ? GroupPowers.AssignMemberLimited
                                : GroupPowers.AssignMember,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
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

                    // Get the group ban list.
                    Dictionary<UUID, DateTime> bannedAgents = null;
                    if (
                        !Services.GetGroupBans(Client, groupUUID, corradeConfiguration.ServicesTimeout,
                            ref bannedAgents))
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_RETRIEVE_GROUP_BAN_LIST);

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
                            // Check if they are in the group already.
                            switch (groupMembers.AsParallel().Any(p => p.Value.ID.Equals(agentUUID)))
                            {
                                case true: // if they are add to the returned list
                                    lock (LockObject)
                                    {
                                        if (!data.Contains(o))
                                            data.Add(o);
                                    }
                                    break;

                                default:
                                    // Check that the agent has not been banned and we have permissions to unban.
                                    if (bannedAgents.ContainsKey(agentUUID) &&
                                        !Services.HasGroupPowers(Client, Client.Self.AgentID, groupUUID,
                                            GroupPowers.GroupBanAccess, corradeConfiguration.ServicesTimeout,
                                            corradeConfiguration.DataTimeout,
                                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                                    {
                                        lock (LockObject)
                                        {
                                            if (!data.Contains(o))
                                                data.Add(o);
                                        }
                                        return;
                                    }
                                    // Check if the avatar is soft-banned.
                                    lock (GroupSoftBansLock)
                                    {
                                        switch (
                                            GroupSoftBans.ContainsKey(groupUUID) &&
                                            GroupSoftBans[groupUUID].AsParallel().Any(p => p.Agent.Equals(agentUUID)))
                                        {
                                            case true:
                                                // If the avatar is banned and soft is not true then do not invite the avatar.
                                                bool soft;
                                                if (!bool.TryParse(wasInput(
                                                        KeyValue.Get(
                                                            wasOutput(
                                                                Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                                                    .SOFT)),
                                                            corradeCommandParameters.Message)), out soft) || !soft)
                                                {
                                                    lock (LockObject)
                                                    {
                                                        if (!data.Contains(o))
                                                            data.Add(o);
                                                    }
                                                    return;
                                                }
                                                // If soft is true then soft-unban the agent before inviting the avatar.
                                                GroupSoftBans[groupUUID].RemoveWhere(p => p.Agent.Equals(agentUUID));
                                                SaveGroupSoftBansState.Invoke();
                                                break;
                                        }
                                    }
                                    Client.Groups.Invite(groupUUID, roleUUIDs.ToList(), agentUUID);
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