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
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> batchinvite =
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
                            GroupPowers.Invite,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    // Get the roles to invite to.
                    HashSet<UUID> roleUUIDs = new HashSet<UUID>();
                    bool rolesFound = true;
                    Parallel.ForEach(CSV.ToEnumerable(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ROLE)),
                                corradeCommandParameters.Message)))
                        .ToArray().AsParallel().Where(o => !string.IsNullOrEmpty(o)), (o, s) =>
                        {
                            UUID roleUUID;
                            if (!UUID.TryParse(o, out roleUUID) &&
                                !Resolvers.RoleNameToUUID(Client, o, corradeCommandParameters.Group.UUID,
                                    corradeConfiguration.ServicesTimeout, ref roleUUID))
                            {
                                rolesFound = false;
                                s.Break();
                            }
                            if (!roleUUIDs.Contains(roleUUID))
                            {
                                roleUUIDs.Add(roleUUID);
                            }
                        });
                    if (!rolesFound)
                        throw new ScriptException(ScriptError.ROLE_NOT_FOUND);
                    // No roles specified, so assume everyone role.
                    if (!roleUUIDs.Any())
                    {
                        roleUUIDs.Add(UUID.Zero);
                    }
                    // If we are not inviting to the everyone role, then check whether we need the group power to
                    // assign just to the roles we are part of or whether we need the power to invite to any role.
                    if (!roleUUIDs.All(o => o.Equals(UUID.Zero)))
                    {
                        // get our current roles.
                        HashSet<UUID> selfRoles = new HashSet<UUID>();
                        ManualResetEvent GroupRoleMembersReplyEvent = new ManualResetEvent(false);
                        EventHandler<GroupRolesMembersReplyEventArgs> GroupRolesMembersEventHandler = (sender, args) =>
                        {
                            selfRoles.UnionWith(
                                args.RolesMembers.ToArray()
                                    .AsParallel()
                                    .Where(o => o.Value.Equals(Client.Self.AgentID))
                                    .Select(o => o.Key));
                            GroupRoleMembersReplyEvent.Set();
                        };
                        lock (Locks.ClientInstanceGroupsLock)
                        {
                            Client.Groups.GroupRoleMembersReply += GroupRolesMembersEventHandler;
                            Client.Groups.RequestGroupRolesMembers(corradeCommandParameters.Group.UUID);
                            if (!GroupRoleMembersReplyEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, false))
                            {
                                Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                                throw new ScriptException(ScriptError.TIMEOUT_GETING_GROUP_ROLES_MEMBERS);
                            }
                            Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                        }
                        if (!Services.HasGroupPowers(Client, Client.Self.AgentID,
                            corradeCommandParameters.Group.UUID,
                            roleUUIDs.All(o => selfRoles.Contains(o))
                                ? GroupPowers.AssignMemberLimited
                                : GroupPowers.AssignMember,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
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
                            // Check if they are in the group already.
                            switch (groupMembers.AsParallel().Any(p => p.Value.ID.Equals(agentUUID)))
                            {
                                case true: // if they are add to the returned list
                                    lock (LockObject)
                                    {
                                        data.Add(o);
                                    }
                                    break;
                                default:
                                    Client.Groups.Invite(corradeCommandParameters.Group.UUID, roleUUIDs.ToList(),
                                        agentUUID);
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