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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> eject =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Group))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    if (
                        !Services.HasGroupPowers(Client, Client.Self.AgentID, corradeCommandParameters.Group.UUID,
                            GroupPowers.Eject,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType)) ||
                        !Services.HasGroupPowers(Client, Client.Self.AgentID, corradeCommandParameters.Group.UUID,
                            GroupPowers.RemoveMember,
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
                    if (
                        !Services.AgentInGroup(Client, agentUUID, corradeCommandParameters.Group.UUID,
                            corradeConfiguration.ServicesTimeout))
                        throw new Command.ScriptException(Enumerations.ScriptError.NOT_IN_GROUP);
                    var targetGroup = new Group();
                    if (
                        !Services.RequestGroup(Client, corradeCommandParameters.Group.UUID,
                            corradeConfiguration.ServicesTimeout,
                            ref targetGroup))
                        throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                    var GroupRoleMembersReplyEvent = new ManualResetEventSlim(false);
                    var rolesMembers = new List<KeyValuePair<UUID, UUID>>();
                    var requestUUID = UUID.Zero;
                    EventHandler<GroupRolesMembersReplyEventArgs> GroupRoleMembersEventHandler = (sender, args) =>
                    {
                        if (!requestUUID.Equals(args.RequestID) ||
                            !args.GroupID.Equals(corradeCommandParameters.Group.UUID))
                            return;
                        rolesMembers = args.RolesMembers;
                        GroupRoleMembersReplyEvent.Set();
                    };
                    Client.Groups.GroupRoleMembersReply += GroupRoleMembersEventHandler;
                    requestUUID =
                        Client.Groups.RequestGroupRolesMembers(corradeCommandParameters.Group.UUID);
                    if (!GroupRoleMembersReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_GROUP_ROLE_MEMBERS);
                    }
                    Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                    var demote = true;
                    if ((!bool.TryParse(
                             wasInput(KeyValue.Get(
                                 wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DEMOTE)),
                                 corradeCommandParameters.Message)),
                             out demote) || !demote) &&
                        !rolesMembers.AsParallel()
                            .Where(o => o.Value.Equals(agentUUID))
                            .All(o => o.Key.Equals(UUID.Zero)))
                        throw new Command.ScriptException(Enumerations.ScriptError.EJECT_NEEDS_DEMOTE);
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
                            throw new Command.ScriptException(Enumerations.ScriptError.CANNOT_EJECT_OWNERS);
                    }
                    var GroupEjectEvent = new ManualResetEventSlim(false);
                    var succeeded = false;
                    EventHandler<GroupOperationEventArgs> GroupOperationEventHandler = (sender, args) =>
                    {
                        if (!args.GroupID.Equals(corradeCommandParameters.Group.UUID))
                            return;
                        succeeded = args.Success;
                        GroupEjectEvent.Set();
                    };
                    Locks.ClientInstanceGroupsLock.EnterWriteLock();
                    Client.Groups.GroupMemberEjected += GroupOperationEventHandler;
                    Client.Groups.EjectUser(corradeCommandParameters.Group.UUID, agentUUID);
                    if (!GroupEjectEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Groups.GroupMemberEjected -= GroupOperationEventHandler;
                        Locks.ClientInstanceGroupsLock.ExitWriteLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_EJECTING_AGENT);
                    }
                    Client.Groups.GroupMemberEjected -= GroupOperationEventHandler;
                    Locks.ClientInstanceGroupsLock.ExitWriteLock();
                    if (!succeeded)
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_EJECT_AGENT);
                };
        }
    }
}