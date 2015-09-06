using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> eject = (commandGroup, message, result) =>
            {
                if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Group))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                if (
                    !HasGroupPowers(Client.Self.AgentID, commandGroup.UUID, GroupPowers.Eject,
                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout) ||
                    !HasGroupPowers(Client.Self.AgentID, commandGroup.UUID, GroupPowers.RemoveMember,
                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                {
                    throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                }
                UUID agentUUID;
                if (
                    !UUID.TryParse(
                        wasInput(wasKeyValueGet(
                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT)), message)),
                        out agentUUID) && !AgentNameToUUID(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME)),
                                    message)),
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME)),
                                    message)),
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            ref agentUUID))
                {
                    throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                }
                if (
                    !AgentInGroup(agentUUID, commandGroup.UUID, corradeConfiguration.ServicesTimeout))
                {
                    throw new ScriptException(ScriptError.NOT_IN_GROUP);
                }
                OpenMetaverse.Group targetGroup = new OpenMetaverse.Group();
                if (!RequestGroup(commandGroup.UUID, corradeConfiguration.ServicesTimeout, ref targetGroup))
                {
                    throw new ScriptException(ScriptError.GROUP_NOT_FOUND);
                }
                ManualResetEvent GroupRoleMembersReplyEvent = new ManualResetEvent(false);
                EventHandler<GroupRolesMembersReplyEventArgs> GroupRoleMembersEventHandler = (sender, args) =>
                {
                    switch (
                        !args.RolesMembers.AsParallel()
                            .Any(o => o.Key.Equals(targetGroup.OwnerRole) && o.Value.Equals(agentUUID)))
                    {
                        case false:
                            throw new ScriptException(ScriptError.CANNOT_EJECT_OWNERS);
                    }
                    Parallel.ForEach(
                        args.RolesMembers.AsParallel().Where(
                            o => o.Value.Equals(agentUUID)),
                        o => Client.Groups.RemoveFromRole(commandGroup.UUID, o.Key, agentUUID));
                    GroupRoleMembersReplyEvent.Set();
                };
                lock (ClientInstanceGroupsLock)
                {
                    Client.Groups.GroupRoleMembersReply += GroupRoleMembersEventHandler;
                    Client.Groups.RequestGroupRolesMembers(commandGroup.UUID);
                    if (!GroupRoleMembersReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                    {
                        Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                        throw new ScriptException(ScriptError.TIMEOUT_GETTING_GROUP_ROLE_MEMBERS);
                    }
                    Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                }
                ManualResetEvent GroupEjectEvent = new ManualResetEvent(false);
                bool succeeded = false;
                EventHandler<GroupOperationEventArgs> GroupOperationEventHandler = (sender, args) =>
                {
                    succeeded = args.Success;
                    GroupEjectEvent.Set();
                };
                lock (ClientInstanceGroupsLock)
                {
                    Client.Groups.GroupMemberEjected += GroupOperationEventHandler;
                    Client.Groups.EjectUser(commandGroup.UUID, agentUUID);
                    if (!GroupEjectEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                    {
                        Client.Groups.GroupMemberEjected -= GroupOperationEventHandler;
                        throw new ScriptException(ScriptError.TIMEOUT_EJECTING_AGENT);
                    }
                    Client.Groups.GroupMemberEjected -= GroupOperationEventHandler;
                }
                if (!succeeded)
                {
                    throw new ScriptException(ScriptError.COULD_NOT_EJECT_AGENT);
                }
            };
        }
    }
}