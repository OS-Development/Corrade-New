///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Corrade.Constants;
using Corrade.Structures;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Collections.Specialized;
using wasSharp.Timers;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> ban =
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
                            new DecayingAlarm(corradeConfiguration.DataDecayType)) ||
                        !Services.HasGroupPowers(Client, Client.Self.AgentID, groupUUID,
                            GroupPowers.GroupBanAccess,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    var action = Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                    );
                    var succeeded = false;
                    var LockObject = new object();
                    Dictionary<UUID, DateTime> bannedAgents = null;
                    switch (action)
                    {
                        case Enumerations.Action.BAN:
                        case Enumerations.Action.UNBAN:
                            var AvatarsLock = new object();
                            var avatars = new Dictionary<UUID, string>();
                            var data = new HashSet<string>();
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
                                                new DecayingAlarm(corradeConfiguration.DataDecayType),
                                                ref agentUUID))
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
                                    lock (AvatarsLock)
                                    {
                                        if (!avatars.ContainsKey(agentUUID))
                                            avatars.Add(agentUUID, o);
                                    }
                                });
                            if (!avatars.Any())
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_AVATARS_TO_BAN_OR_UNBAN);

                            // request current banned agents
                            if (
                                !Services.GetGroupBans(Client, groupUUID, corradeConfiguration.ServicesTimeout,
                                    ref bannedAgents))
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.COULD_NOT_RETRIEVE_GROUP_BAN_LIST);

                            // clean ban list
                            switch (action)
                            {
                                case Enumerations.Action.BAN:
                                    // only ban avatars that are not already banned
                                    lock (AvatarsLock)
                                    {
                                        avatars =
                                            avatars.AsParallel()
                                                .Where(o => !bannedAgents.ContainsKey(o.Key))
                                                .GroupBy(o => o.Key)
                                                .ToDictionary(o => o.Key, o => o.FirstOrDefault().Value);
                                    }
                                    break;

                                case Enumerations.Action.UNBAN:
                                    // only unban avatars that are already banned
                                    lock (AvatarsLock)
                                    {
                                        avatars =
                                            avatars.AsParallel()
                                                .Where(o => bannedAgents.ContainsKey(o.Key))
                                                .GroupBy(o => o.Key)
                                                .ToDictionary(o => o.Key, o => o.FirstOrDefault().Value);
                                    }
                                    break;
                            }

                            // check whether added bans would not exceed the maximum ban list in Second Life
                            if (action.Equals(Enumerations.Action.BAN) &&
                                wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                                bannedAgents.Count + avatars.Count >
                                wasOpenMetaverse.Constants.GROUPS.MAXIMUM_GROUP_BANS)
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.BAN_WOULD_EXCEED_MAXIMUM_BAN_LIST_LENGTH);

                            // ban or unban the avatars
                            var GroupBanEvent = new ManualResetEventSlim(false);
                            switch (action)
                            {
                                case Enumerations.Action.BAN:
                                    Client.Groups.RequestBanAction(groupUUID,
                                        GroupBanAction.Ban,
                                        avatars.Keys.ToArray(), (sender, args) => { GroupBanEvent.Set(); });
                                    break;

                                case Enumerations.Action.UNBAN:
                                    Client.Groups.RequestBanAction(groupUUID,
                                        GroupBanAction.Unban,
                                        avatars.Keys.ToArray(), (sender, args) => { GroupBanEvent.Set(); });
                                    break;
                            }
                            if (!GroupBanEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_MODIFYING_GROUP_BAN_LIST);

                            // also soft ban if requested
                            var groupSoftBansModified = false;
                            bool soft;
                            switch (bool.TryParse(wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SOFT)),
                                            corradeCommandParameters.Message)), out soft) && soft)
                            {
                                case true:
                                    switch (action)
                                    {
                                        case Enumerations.Action.BAN:
                                            avatars.AsParallel().ForAll(o =>
                                            {
                                                var fullName = wasOpenMetaverse.Helpers.GetAvatarNames(o.Value);
                                                var enumerable = fullName as IList<string> ?? fullName.ToList();
                                                if (!enumerable.Any())
                                                    return;
                                                var softBan = new SoftBan
                                                {
                                                    Agent = o.Key,
                                                    FirstName = enumerable.First(),
                                                    LastName = enumerable.Last(),
                                                    Timestamp =
                                                        DateTime.UtcNow.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP),
                                                    Last = DateTime.UtcNow.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP)
                                                };
                                                lock (GroupSoftBansLock)
                                                {
                                                    switch (GroupSoftBans.ContainsKey(groupUUID))
                                                    {
                                                        case true:
                                                            if (
                                                                GroupSoftBans[groupUUID].AsParallel()
                                                                    .Any(p => p.Agent.Equals(o.Key)))
                                                                return;
                                                            GroupSoftBans[groupUUID].Add(softBan);
                                                            groupSoftBansModified = true;
                                                            break;

                                                        default:
                                                            GroupSoftBans.Add(groupUUID,
                                                                new ObservableHashSet<SoftBan>());
                                                            GroupSoftBans[groupUUID].CollectionChanged +=
                                                                HandleGroupSoftBansChanged;
                                                            GroupSoftBans[groupUUID].Add(softBan);
                                                            groupSoftBansModified = true;
                                                            break;
                                                    }
                                                }
                                            });
                                            break;

                                        case Enumerations.Action.UNBAN:
                                            avatars.Keys.AsParallel().ForAll(o =>
                                            {
                                                lock (GroupSoftBansLock)
                                                {
                                                    switch (GroupSoftBans.ContainsKey(groupUUID))
                                                    {
                                                        case true:
                                                            if (
                                                                !GroupSoftBans[groupUUID].AsParallel()
                                                                    .Any(p => p.Agent.Equals(o)))
                                                                return;
                                                            GroupSoftBans[groupUUID]
                                                                .RemoveWhere(p => p.Agent.Equals(o));
                                                            groupSoftBansModified = true;
                                                            break;
                                                    }
                                                }
                                            });
                                            break;
                                    }
                                    if (groupSoftBansModified)
                                        SaveGroupSoftBansState.Invoke();
                                    break;
                            }

                            // if this is a ban request and eject was requested as well, then eject the agents.
                            switch (action)
                            {
                                case Enumerations.Action.BAN:
                                    bool alsoEject;
                                    if (!bool.TryParse(
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                                        .EJECT)),
                                                    corradeCommandParameters.Message)),
                                            out alsoEject) || alsoEject == false) break;

                                    // Get the group members.
                                    Dictionary<UUID, GroupMember> groupMembers = null;
                                    var groupMembersReceivedEvent = new ManualResetEventSlim(false);
                                    var groupMembersRequestUUID = UUID.Zero;
                                    EventHandler<GroupMembersReplyEventArgs> HandleGroupMembersReplyDelegate =
                                        (sender, args) =>
                                        {
                                            if (!groupMembersRequestUUID.Equals(args.RequestID)) return;

                                            groupMembers = args.Members;
                                            groupMembersReceivedEvent.Set();
                                        };

                                    Client.Groups.GroupMembersReply += HandleGroupMembersReplyDelegate;
                                    groupMembersRequestUUID = Client.Groups.RequestGroupMembers(groupUUID);
                                    if (
                                        !groupMembersReceivedEvent.Wait(
                                            (int) corradeConfiguration.ServicesTimeout))
                                    {
                                        Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.TIMEOUT_GETTING_GROUP_MEMBERS);
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
                                    EventHandler<GroupRolesMembersReplyEventArgs> GroupRoleMembersEventHandler =
                                        (sender, args) =>
                                        {
                                            if (!groupRolesMembersRequestUUID.Equals(args.RequestID)) return;
                                            groupRolesMembers = args.RolesMembers;
                                            GroupRoleMembersReplyEvent.Set();
                                        };
                                    Client.Groups.GroupRoleMembersReply += GroupRoleMembersEventHandler;
                                    groupRolesMembersRequestUUID = Client.Groups.RequestGroupRolesMembers(groupUUID);
                                    if (
                                        !GroupRoleMembersReplyEvent.Wait(
                                            (int) corradeConfiguration.ServicesTimeout))
                                    {
                                        Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.TIMEOUT_GETTING_GROUP_ROLE_MEMBERS);
                                    }
                                    Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                                    groupMembers
                                        .AsParallel()
                                        .Where(o => avatars.ContainsKey(o.Value.ID))
                                        .ForAll(
                                            o =>
                                            {
                                                // Check their status.
                                                if (
                                                    groupRolesMembers.AsParallel()
                                                        .Any(
                                                            p =>
                                                                p.Key.Equals(targetGroup.OwnerRole) &&
                                                                p.Value.Equals(o.Value.ID)))
                                                {
                                                    lock (LockObject)
                                                    {
                                                        if (!data.Contains(avatars[o.Value.ID]))
                                                            data.Add(avatars[o.Value.ID]);
                                                    }
                                                    return;
                                                }
                                                // Demote them.
                                                groupRolesMembers.AsParallel().Where(
                                                    p => p.Value.Equals(o.Value.ID)).ForAll(p =>
                                                    Client.Groups.RemoveFromRole(
                                                        groupUUID, p.Key,
                                                        o.Value.ID));
                                                var GroupEjectEvent = new ManualResetEventSlim(false);
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
                                                Client.Groups.EjectUser(groupUUID,
                                                    o.Value.ID);
                                                GroupEjectEvent.Wait(
                                                    (int) corradeConfiguration.ServicesTimeout);
                                                Client.Groups.GroupMemberEjected -= GroupOperationEventHandler;
                                                Locks.ClientInstanceGroupsLock.ExitWriteLock();
                                                // If the eject was not successful, add them to the output.
                                                switch (succeeded)
                                                {
                                                    case false:
                                                        lock (LockObject)
                                                        {
                                                            if (!data.Contains(avatars[o.Value.ID]))
                                                                data.Add(avatars[o.Value.ID]);
                                                        }
                                                        break;
                                                }
                                            });

                                    break;
                            }
                            if (data.Any())
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                    CSV.FromEnumerable(data));
                            break;

                        case Enumerations.Action.LIST:
                            if (
                                !Services.GetGroupBans(Client, groupUUID, corradeConfiguration.ServicesTimeout,
                                    ref bannedAgents))
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.COULD_NOT_RETRIEVE_GROUP_BAN_LIST);
                            var csv = new List<string>();
                            switch (bannedAgents != null)
                            {
                                case true:
                                    bannedAgents.AsParallel().ForAll(o =>
                                    {
                                        var agentName = string.Empty;
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
                                                        o.Value.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP));
                                                }
                                                break;
                                        }
                                    });
                                    break;

                                default:
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.COULD_NOT_RETRIEVE_GROUP_BAN_LIST);
                            }
                            if (csv.Any())
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                    CSV.FromEnumerable(csv));
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}