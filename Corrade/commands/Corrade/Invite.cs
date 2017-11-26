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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> invite =
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
                    if (
                        !Services.HasGroupPowers(Client, Client.Self.AgentID, groupUUID,
                            GroupPowers.Invite,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
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
                    // If verify is true then check that the agent is not already in the group.
                    bool verify;
                    if (bool.TryParse(wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.VERIFY)),
                                corradeCommandParameters.Message)), out verify) && verify)
                        if (Services.AgentInGroup(Client, agentUUID, groupUUID,
                            corradeConfiguration.ServicesTimeout))
                            throw new Command.ScriptException(Enumerations.ScriptError.ALREADY_IN_GROUP);

                    // Get the group ban list.
                    Dictionary<UUID, DateTime> bannedAgents = null;
                    if (
                        !Services.GetGroupBans(Client, groupUUID, corradeConfiguration.ServicesTimeout,
                            ref bannedAgents))
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_RETRIEVE_GROUP_BAN_LIST);
                    // Check that the agent has not been banned.
                    if (bannedAgents.ContainsKey(agentUUID) &&
                        !Services.HasGroupPowers(Client, Client.Self.AgentID, groupUUID,
                            GroupPowers.GroupBanAccess,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);

                    // Check if the avatar is soft-banned.
                    lock (GroupSoftBansLock)
                    {
                        switch (
                            GroupSoftBans.ContainsKey(groupUUID) &&
                            GroupSoftBans[groupUUID].AsParallel().Any(o => o.Agent.Equals(agentUUID)))
                        {
                            case true:
                                // If the avatar is banned and soft is not true then do not invite the avatar.
                                bool soft;
                                if (!bool.TryParse(wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SOFT)),
                                            corradeCommandParameters.Message)), out soft) || !soft)
                                    throw new Command.ScriptException(Enumerations.ScriptError.AGENT_IS_SOFT_BANNED);
                                // If soft is true then soft-unban the agent before inviting the avatar.
                                GroupSoftBans[groupUUID].RemoveWhere(o => o.Agent.Equals(agentUUID));
                                SaveGroupSoftBansState.Invoke();
                                break;
                        }
                    }

                    var roleUUIDs = new HashSet<UUID>();
                    var LockObject = new object();
                    var rolesFound = true;
                    Parallel.ForEach(CSV.ToEnumerable(
                            wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ROLE)),
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
                        lock (LockObject)
                        {
                            if (!roleUUIDs.Contains(roleUUID))
                                roleUUIDs.Add(roleUUID);
                        }
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
                    Client.Groups.Invite(groupUUID, roleUUIDs.ToList(), agentUUID);
                };
        }
    }
}