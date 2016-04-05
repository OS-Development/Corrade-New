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
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> ban =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name, (int) Configuration.Permissions.Group))
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
                            GroupPowers.GroupBanAccess,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    Action action = Reflection.GetEnumValueFromName<Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant());
                    object LockObject = new object();
                    bool succeeded = false;
                    switch (action)
                    {
                        case Action.BAN:
                        case Action.UNBAN:
                            object AvatarsLock = new object();
                            Dictionary<UUID, string> avatars = new Dictionary<UUID, string>();
                            HashSet<string> data = new HashSet<string>();
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
                                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType),
                                                ref agentUUID))
                                        {
                                            // Add all the unrecognized agents to the returned list.
                                            lock (LockObject)
                                            {
                                                data.Add(o);
                                            }
                                            return;
                                        }
                                    }
                                    lock (AvatarsLock)
                                    {
                                        avatars.Add(agentUUID, o);
                                    }
                                });
                            if (!avatars.Any())
                                throw new ScriptException(ScriptError.NO_AVATARS_TO_BAN_OR_UNBAN);
                            // ban or unban the avatars
                            lock (Locks.ClientInstanceGroupsLock)
                            {
                                ManualResetEvent GroupBanEvent = new ManualResetEvent(false);
                                switch (action)
                                {
                                    case Action.BAN:
                                        Client.Groups.RequestBanAction(corradeCommandParameters.Group.UUID,
                                            GroupBanAction.Ban,
                                            avatars.Keys.ToArray(), (sender, args) => { GroupBanEvent.Set(); });
                                        break;
                                    case Action.UNBAN:
                                        Client.Groups.RequestBanAction(corradeCommandParameters.Group.UUID,
                                            GroupBanAction.Unban,
                                            avatars.Keys.ToArray(), (sender, args) => { GroupBanEvent.Set(); });
                                        break;
                                }
                                if (!GroupBanEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    throw new ScriptException(ScriptError.TIMEOUT_MODIFYING_GROUP_BAN_LIST);
                                }
                            }
                            // if this is a ban request and eject was requested as well, then eject the agents.
                            switch (action)
                            {
                                case Action.BAN:
                                    bool alsoeject;
                                    if (bool.TryParse(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.EJECT)),
                                                corradeCommandParameters.Message)),
                                        out alsoeject) && alsoeject)
                                    {
                                        // Get the group members.
                                        Dictionary<UUID, GroupMember> groupMembers = null;
                                        ManualResetEvent groupMembersReceivedEvent = new ManualResetEvent(false);
                                        EventHandler<GroupMembersReplyEventArgs> HandleGroupMembersReplyDelegate =
                                            (sender, args) =>
                                            {
                                                groupMembers = args.Members;
                                                groupMembersReceivedEvent.Set();
                                            };
                                        lock (Locks.ClientInstanceGroupsLock)
                                        {
                                            Client.Groups.GroupMembersReply += HandleGroupMembersReplyDelegate;
                                            Client.Groups.RequestGroupMembers(corradeCommandParameters.Group.UUID);
                                            if (
                                                !groupMembersReceivedEvent.WaitOne(
                                                    (int) corradeConfiguration.ServicesTimeout, false))
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
                                        EventHandler<GroupRolesMembersReplyEventArgs> GroupRoleMembersEventHandler =
                                            (sender, args) =>
                                            {
                                                groupRolesMembers = args.RolesMembers;
                                                GroupRoleMembersReplyEvent.Set();
                                            };
                                        lock (Locks.ClientInstanceGroupsLock)
                                        {
                                            Client.Groups.GroupRoleMembersReply += GroupRoleMembersEventHandler;
                                            Client.Groups.RequestGroupRolesMembers(corradeCommandParameters.Group.UUID);
                                            if (
                                                !GroupRoleMembersReplyEvent.WaitOne(
                                                    (int) corradeConfiguration.ServicesTimeout, false))
                                            {
                                                Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                                                throw new ScriptException(
                                                    ScriptError.TIMEOUT_GETTING_GROUP_ROLE_MEMBERS);
                                            }
                                            Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                                        }
                                        groupMembers.ToArray()
                                            .AsParallel()
                                            .Where(o => avatars.ContainsKey(o.Value.ID))
                                            .ForAll(
                                                o =>
                                                {
                                                    // Check their status.
                                                    switch (
                                                        !groupRolesMembers.ToArray().AsParallel()
                                                            .Any(
                                                                p =>
                                                                    p.Key.Equals(targetGroup.OwnerRole) &&
                                                                    p.Value.Equals(o.Value.ID))
                                                        )
                                                    {
                                                        case false: // cannot demote owners
                                                            lock (LockObject)
                                                            {
                                                                data.Add(avatars[o.Value.ID]);
                                                            }
                                                            return;
                                                    }
                                                    // Demote them.
                                                    groupRolesMembers.ToArray().AsParallel().Where(
                                                        p => p.Value.Equals(o.Value.ID)).ForAll(p =>
                                                            Client.Groups.RemoveFromRole(
                                                                corradeCommandParameters.Group.UUID, p.Key,
                                                                o.Value.ID));
                                                    ManualResetEvent GroupEjectEvent = new ManualResetEvent(false);
                                                    EventHandler<GroupOperationEventArgs> GroupOperationEventHandler =
                                                        (sender, args) =>
                                                        {
                                                            succeeded = args.Success;
                                                            GroupEjectEvent.Set();
                                                        };
                                                    lock (Locks.ClientInstanceGroupsLock)
                                                    {
                                                        Client.Groups.GroupMemberEjected += GroupOperationEventHandler;
                                                        Client.Groups.EjectUser(corradeCommandParameters.Group.UUID,
                                                            o.Value.ID);
                                                        GroupEjectEvent.WaitOne(
                                                            (int) corradeConfiguration.ServicesTimeout,
                                                            false);
                                                        Client.Groups.GroupMemberEjected -= GroupOperationEventHandler;
                                                    }
                                                    // If the eject was not successful, add them to the output.
                                                    switch (succeeded)
                                                    {
                                                        case false:
                                                            lock (LockObject)
                                                            {
                                                                data.Add(avatars[o.Value.ID]);
                                                            }
                                                            break;
                                                    }
                                                });
                                    }
                                    break;
                            }
                            if (data.Any())
                            {
                                result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                                    CSV.FromEnumerable(data));
                            }
                            break;
                        case Action.LIST:
                            ManualResetEvent BannedAgentsEvent = new ManualResetEvent(false);
                            Dictionary<UUID, DateTime> bannedAgents = null;
                            EventHandler<BannedAgentsEventArgs> BannedAgentsEventHandler = (sender, args) =>
                            {
                                succeeded = args.Success;
                                bannedAgents = args.BannedAgents;
                                BannedAgentsEvent.Set();
                            };
                            lock (Locks.ClientInstanceGroupsLock)
                            {
                                Client.Groups.BannedAgents += BannedAgentsEventHandler;
                                Client.Groups.RequestBannedAgents(corradeCommandParameters.Group.UUID);
                                if (!BannedAgentsEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    Client.Groups.BannedAgents -= BannedAgentsEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_RETRIEVING_GROUP_BAN_LIST);
                                }
                                Client.Groups.BannedAgents -= BannedAgentsEventHandler;
                            }
                            List<string> csv = new List<string>();
                            switch (succeeded && bannedAgents != null)
                            {
                                case true:
                                    Parallel.ForEach(bannedAgents, o =>
                                    {
                                        string agentName = string.Empty;
                                        switch (
                                            !Resolvers.AgentUUIDToName(Client, o.Key,
                                                corradeConfiguration.ServicesTimeout,
                                                ref agentName))
                                        {
                                            case false:
                                                lock (LockObject)
                                                {
                                                    csv.Add(agentName);
                                                    csv.Add(o.Key.ToString());
                                                    csv.Add(
                                                        o.Value.ToString(Utils.EnUsCulture));
                                                }
                                                break;
                                        }
                                    });
                                    break;
                                default:
                                    throw new ScriptException(ScriptError.COULD_NOT_RETRIEVE_GROUP_BAN_LIST);
                            }
                            if (csv.Any())
                            {
                                result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                                    CSV.FromEnumerable(csv));
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}