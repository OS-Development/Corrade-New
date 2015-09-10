///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
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
            public static Action<Group, string, Dictionary<string, string>> ban = (commandGroup, message, result) =>
            {
                if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Group))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                IEnumerable<UUID> currentGroups = Enumerable.Empty<UUID>();

                if (
                    !GetCurrentGroups(corradeConfiguration.ServicesTimeout,
                        ref currentGroups))
                {
                    throw new ScriptException(ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                }

                if (!new HashSet<UUID>(currentGroups).Contains(commandGroup.UUID))
                {
                    throw new ScriptException(ScriptError.NOT_IN_GROUP);
                }
                if (
                    !HasGroupPowers(Client.Self.AgentID, commandGroup.UUID, GroupPowers.GroupBanAccess,
                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                {
                    throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                }
                Action action = wasGetEnumValueFromDescription<Action>(
                    wasInput(
                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)), message))
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
                        Parallel.ForEach(
                            wasCSVToEnumerable(
                                wasInput(
                                    wasKeyValueGet(
                                        wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.AVATARS)),
                                        message))).AsParallel().Where(o => !string.IsNullOrEmpty(o)), o =>
                                        {
                                            UUID agentUUID;
                                            if (!UUID.TryParse(o, out agentUUID))
                                            {
                                                List<string> fullName = new List<string>(GetAvatarNames(o));
                                                if (
                                                    !AgentNameToUUID(fullName.First(), fullName.Last(),
                                                        corradeConfiguration.ServicesTimeout,
                                                        corradeConfiguration.DataTimeout, ref agentUUID))
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
                        lock (ClientInstanceGroupsLock)
                        {
                            ManualResetEvent GroupBanEvent = new ManualResetEvent(false);
                            switch (action)
                            {
                                case Action.BAN:
                                    Client.Groups.RequestBanAction(commandGroup.UUID, GroupBanAction.Ban,
                                        avatars.Keys.ToArray(), (sender, args) => { GroupBanEvent.Set(); });
                                    break;
                                case Action.UNBAN:
                                    Client.Groups.RequestBanAction(commandGroup.UUID, GroupBanAction.Unban,
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
                                        wasKeyValueGet(
                                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.EJECT)),
                                            message)),
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
                                    lock (ClientInstanceGroupsLock)
                                    {
                                        Client.Groups.GroupMembersReply += HandleGroupMembersReplyDelegate;
                                        Client.Groups.RequestGroupMembers(commandGroup.UUID);
                                        if (
                                            !groupMembersReceivedEvent.WaitOne(
                                                (int) corradeConfiguration.ServicesTimeout, false))
                                        {
                                            Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_GROUP_MEMBERS);
                                        }
                                        Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                                    }
                                    OpenMetaverse.Group targetGroup = new OpenMetaverse.Group();
                                    if (
                                        !RequestGroup(commandGroup.UUID, corradeConfiguration.ServicesTimeout,
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
                                    lock (ClientInstanceGroupsLock)
                                    {
                                        Client.Groups.GroupRoleMembersReply += GroupRoleMembersEventHandler;
                                        Client.Groups.RequestGroupRolesMembers(commandGroup.UUID);
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
                                    Parallel.ForEach(
                                        groupMembers.AsParallel().Where(o => avatars.ContainsKey(o.Value.ID)),
                                        o =>
                                        {
                                            // Check their status.
                                            switch (
                                                !groupRolesMembers.AsParallel()
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
                                            Parallel.ForEach(
                                                groupRolesMembers.AsParallel().Where(
                                                    p => p.Value.Equals(o.Value.ID)),
                                                p =>
                                                    Client.Groups.RemoveFromRole(commandGroup.UUID, p.Key,
                                                        o.Value.ID));
                                            ManualResetEvent GroupEjectEvent = new ManualResetEvent(false);
                                            EventHandler<GroupOperationEventArgs> GroupOperationEventHandler =
                                                (sender, args) =>
                                                {
                                                    succeeded = args.Success;
                                                    GroupEjectEvent.Set();
                                                };
                                            lock (ClientInstanceGroupsLock)
                                            {
                                                Client.Groups.GroupMemberEjected += GroupOperationEventHandler;
                                                Client.Groups.EjectUser(commandGroup.UUID, o.Value.ID);
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
                            result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                                wasEnumerableToCSV(data));
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
                        lock (ClientInstanceGroupsLock)
                        {
                            Client.Groups.BannedAgents += BannedAgentsEventHandler;
                            Client.Groups.RequestBannedAgents(commandGroup.UUID);
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
                                        !AgentUUIDToName(o.Key, corradeConfiguration.ServicesTimeout,
                                            ref agentName))
                                    {
                                        case false:
                                            lock (LockObject)
                                            {
                                                csv.Add(agentName);
                                                csv.Add(o.Key.ToString());
                                                csv.Add(
                                                    o.Value.ToString(CultureInfo.InvariantCulture));
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
                            result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                                wasEnumerableToCSV(csv));
                        }
                        break;
                    default:
                        throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                }
            };
        }
    }
}