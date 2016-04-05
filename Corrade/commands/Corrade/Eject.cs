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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> eject =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name, (int) Configuration.Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
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
                    UUID agentUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT)),
                                corradeCommandParameters.Message)),
                            out agentUUID) && !Resolvers.AgentNameToUUID(Client,
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME)),
                                        corradeCommandParameters.Message)),
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME)),
                                        corradeCommandParameters.Message)),
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType),
                                ref agentUUID))
                    {
                        throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                    }
                    if (
                        !Services.AgentInGroup(Client, agentUUID, corradeCommandParameters.Group.UUID,
                            corradeConfiguration.ServicesTimeout))
                    {
                        throw new ScriptException(ScriptError.NOT_IN_GROUP);
                    }
                    Group targetGroup = new Group();
                    if (
                        !Services.RequestGroup(Client, corradeCommandParameters.Group.UUID,
                            corradeConfiguration.ServicesTimeout,
                            ref targetGroup))
                    {
                        throw new ScriptException(ScriptError.GROUP_NOT_FOUND);
                    }
                    ManualResetEvent GroupRoleMembersReplyEvent = new ManualResetEvent(false);
                    List<KeyValuePair<UUID, UUID>> rolesMembers = new List<KeyValuePair<UUID, UUID>>();
                    EventHandler<GroupRolesMembersReplyEventArgs> GroupRoleMembersEventHandler = (sender, args) =>
                    {
                        rolesMembers = args.RolesMembers;
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
                    lock (Locks.ClientInstanceGroupsLock)
                    {
                        switch (
                            !rolesMembers.AsParallel()
                                .Any(o => o.Key.Equals(targetGroup.OwnerRole) && o.Value.Equals(agentUUID)))
                        {
                            case true:
                                rolesMembers.AsParallel().Where(
                                    o => o.Value.Equals(agentUUID))
                                    .ForAll(
                                        o => Client.Groups.RemoveFromRole(corradeCommandParameters.Group.UUID, o.Key,
                                            agentUUID));
                                break;
                            default:
                                throw new ScriptException(ScriptError.CANNOT_EJECT_OWNERS);
                        }
                    }
                    ManualResetEvent GroupEjectEvent = new ManualResetEvent(false);
                    bool succeeded = false;
                    EventHandler<GroupOperationEventArgs> GroupOperationEventHandler = (sender, args) =>
                    {
                        succeeded = args.Success;
                        GroupEjectEvent.Set();
                    };
                    lock (Locks.ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupMemberEjected += GroupOperationEventHandler;
                        Client.Groups.EjectUser(corradeCommandParameters.Group.UUID, agentUUID);
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