///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Helpers = wasOpenMetaverse.Helpers;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> softban =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    UUID groupUUID;
                    var target = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TARGET)),
                            corradeCommandParameters.Message));
                    switch (string.IsNullOrEmpty(target))
                    {
                        case false:
                            if (!UUID.TryParse(target, out groupUUID) &&
                                !Resolvers.GroupNameToUUID(Client, target, corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout,
                                    new Time.DecayingAlarm(corradeConfiguration.DataDecayType), ref groupUUID))
                                throw new ScriptException(ScriptError.GROUP_NOT_FOUND);
                            break;
                        default:
                            groupUUID = corradeCommandParameters.Group.UUID;
                            break;
                    }
                    var currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                    }

                    if (!new HashSet<UUID>(currentGroups).Contains(groupUUID))
                    {
                        throw new ScriptException(ScriptError.NOT_IN_GROUP);
                    }
                    if (
                        !Services.HasGroupPowers(Client, Client.Self.AgentID, groupUUID,
                            GroupPowers.Eject,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    var action = Reflection.GetEnumValueFromName<Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant());
                    var succeeded = false;
                    var LockObject = new object();
                    switch (action)
                    {
                        case Action.BAN:
                        case Action.UNBAN:
                            var AvatarsLock = new object();
                            var avatars = new Dictionary<UUID, string>();
                            var data = new HashSet<string>();
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
                                        var fullName = new List<string>(Helpers.GetAvatarNames(o));
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
                                                if (!data.Contains(o))
                                                {
                                                    data.Add(o);
                                                }
                                            }
                                            return;
                                        }
                                    }
                                    lock (AvatarsLock)
                                    {
                                        if (!avatars.ContainsKey(agentUUID))
                                        {
                                            avatars.Add(agentUUID, o);
                                        }
                                    }
                                });
                            if (!avatars.Any())
                                throw new ScriptException(ScriptError.NO_AVATARS_TO_BAN_OR_UNBAN);

                            // clean ban list if group is already in the soft ban list
                            if (GroupSoftBans.ContainsKey(groupUUID))
                            {
                                switch (action)
                                {
                                    case Action.BAN:
                                        // only ban avatars that are not already banned
                                        lock (AvatarsLock)
                                        {
                                            avatars =
                                                avatars.AsParallel()
                                                    .Where(o => !GroupSoftBans[groupUUID].Contains(o.Key))
                                                    .ToDictionary(o => o.Key, o => o.Value);
                                        }
                                        break;
                                    case Action.UNBAN:
                                        // only unban avatars that are already banned
                                        lock (AvatarsLock)
                                        {
                                            avatars =
                                                avatars.AsParallel()
                                                    .Where(o => GroupSoftBans[groupUUID].Contains(o.Key))
                                                    .ToDictionary(o => o.Key, o => o.Value);
                                        }
                                        break;
                                }
                            }

                            // ban or unban the avatars
                            var groupSoftBansModified = false;
                            switch (action)
                            {
                                case Action.BAN:
                                    avatars.Keys.AsParallel().ForAll(o =>
                                    {
                                        lock (GroupSoftBansLock)
                                        {
                                            switch (GroupSoftBans.ContainsKey(groupUUID))
                                            {
                                                case true:
                                                    if (GroupSoftBans[groupUUID].Contains(o))
                                                        return;
                                                    GroupSoftBans[groupUUID].Add(o);
                                                    groupSoftBansModified = true;
                                                    break;
                                                default:
                                                    GroupSoftBans.Add(groupUUID,
                                                        new Collections.ObservableHashSet<UUID>());
                                                    GroupSoftBans[groupUUID].CollectionChanged +=
                                                        HandleGroupSoftBansChanged;
                                                    GroupSoftBans[groupUUID].Add(o);
                                                    groupSoftBansModified = true;
                                                    break;
                                            }
                                        }
                                    });
                                    break;
                                case Action.UNBAN:
                                    avatars.Keys.AsParallel().ForAll(o =>
                                    {
                                        lock (GroupSoftBansLock)
                                        {
                                            switch (GroupSoftBans.ContainsKey(groupUUID))
                                            {
                                                case true:
                                                    if (!GroupSoftBans[groupUUID].Contains(o))
                                                        return;
                                                    GroupSoftBans[groupUUID].Remove(o);
                                                    groupSoftBansModified = true;
                                                    break;
                                            }
                                        }
                                    });
                                    break;
                            }

                            // Save the soft ban list.
                            if (groupSoftBansModified)
                                SaveGroupSoftBansState.Invoke();

                            // If this is a ban request and eject was requested as well, then eject the agents.
                            switch (action)
                            {
                                case Action.BAN:
                                    // By default a soft-ban also ejects an agent.
                                    bool alsoeject;
                                    if (!bool.TryParse(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.EJECT)),
                                                corradeCommandParameters.Message)),
                                        out alsoeject) || alsoeject == false) break;
                                    // Get the group members.
                                    Dictionary<UUID, GroupMember> groupMembers = null;
                                    var groupMembersReceivedEvent = new ManualResetEvent(false);
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
                                        !groupMembersReceivedEvent.WaitOne(
                                            (int) corradeConfiguration.ServicesTimeout, false))
                                    {
                                        Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                                        throw new ScriptException(ScriptError.TIMEOUT_GETTING_GROUP_MEMBERS);
                                    }
                                    Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;

                                    var targetGroup = new Group();
                                    if (
                                        !Services.RequestGroup(Client, groupUUID,
                                            corradeConfiguration.ServicesTimeout,
                                            ref targetGroup))
                                    {
                                        throw new ScriptException(ScriptError.GROUP_NOT_FOUND);
                                    }
                                    // Get roles members.
                                    List<KeyValuePair<UUID, UUID>> groupRolesMembers = null;
                                    var GroupRoleMembersReplyEvent = new ManualResetEvent(false);
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
                                        !GroupRoleMembersReplyEvent.WaitOne(
                                            (int) corradeConfiguration.ServicesTimeout, false))
                                    {
                                        Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                                        throw new ScriptException(
                                            ScriptError.TIMEOUT_GETTING_GROUP_ROLE_MEMBERS);
                                    }
                                    Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                                    groupMembers
                                        .AsParallel()
                                        .Where(o => avatars.ContainsKey(o.Value.ID))
                                        .ForAll(
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
                                                            if (!data.Contains(avatars[o.Value.ID]))
                                                            {
                                                                data.Add(avatars[o.Value.ID]);
                                                            }
                                                        }
                                                        return;
                                                }
                                                // Demote them.
                                                groupRolesMembers.AsParallel().Where(
                                                    p => p.Value.Equals(o.Value.ID)).ForAll(p =>
                                                        Client.Groups.RemoveFromRole(
                                                            groupUUID, p.Key,
                                                            o.Value.ID));
                                                var GroupEjectEvent = new ManualResetEvent(false);
                                                EventHandler<GroupOperationEventArgs> GroupOperationEventHandler =
                                                    (sender, args) =>
                                                    {
                                                        succeeded = args.Success;
                                                        GroupEjectEvent.Set();
                                                    };
                                                lock (Locks.ClientInstanceGroupsLock)
                                                {
                                                    Client.Groups.GroupMemberEjected += GroupOperationEventHandler;
                                                    Client.Groups.EjectUser(groupUUID,
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
                                                            if (!data.Contains(avatars[o.Value.ID]))
                                                            {
                                                                data.Add(avatars[o.Value.ID]);
                                                            }
                                                        }
                                                        break;
                                                }
                                            });

                                    break;
                            }
                            if (data.Any())
                            {
                                result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                                    CSV.FromEnumerable(data));
                            }
                            break;
                        case Action.LIST:
                            var csv = new List<string>();
                            lock (GroupSoftBansLock)
                            {
                                if (GroupSoftBans.ContainsKey(groupUUID))
                                {
                                    GroupSoftBans[groupUUID].AsParallel().ForAll(o =>
                                    {
                                        var agentName = string.Empty;
                                        if (
                                            !Resolvers.AgentUUIDToName(Client, o,
                                                corradeConfiguration.ServicesTimeout,
                                                ref agentName))
                                            return;
                                        lock (LockObject)
                                        {
                                            csv.Add(agentName);
                                            csv.Add(o.ToString());
                                        }
                                    });
                                }
                            }
                            if (csv.Any())
                            {
                                result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                                    CSV.FromEnumerable(csv));
                            }
                            break;
                        case Action.IMPORT:
                            switch (Reflection.GetEnumValueFromName<Entity>(
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY)),
                                        corradeCommandParameters.Message))
                                    .ToLowerInvariant()))
                            {
                                case Entity.GROUP:
                                    Dictionary<UUID, DateTime> bannedAgents = null;
                                    if (
                                        !Services.GetGroupBans(Client, groupUUID, corradeConfiguration.ServicesTimeout,
                                            ref bannedAgents))
                                    {
                                        throw new ScriptException(ScriptError.COULD_NOT_RETRIEVE_GROUP_BAN_LIST);
                                    }
                                    if (bannedAgents == null)
                                        throw new ScriptException(ScriptError.COULD_NOT_RETRIEVE_GROUP_BAN_LIST);
                                    lock (GroupSoftBansLock)
                                    {
                                        switch (!GroupSoftBans.ContainsKey(groupUUID))
                                        {
                                            case true:
                                                GroupSoftBans.Add(groupUUID,
                                                    new Collections.ObservableHashSet<UUID>());
                                                GroupSoftBans[groupUUID].CollectionChanged += HandleGroupSoftBansChanged;
                                                GroupSoftBans[groupUUID].UnionWith(bannedAgents.Keys.AsEnumerable());
                                                break;
                                            default:
                                                GroupSoftBans[groupUUID].UnionWith(bannedAgents.Keys.AsEnumerable());
                                                break;
                                        }
                                    }
                                    SaveGroupSoftBansState.Invoke();
                                    break;
                                case Entity.MUTE:
                                    if (
                                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                            (int) Configuration.Permissions.Mute))
                                    {
                                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                                    }
                                    var mutes = Enumerable.Empty<MuteEntry>();
                                    // retrieve the current mute list
                                    switch (Cache.MuteCache.IsVirgin)
                                    {
                                        case true:
                                            if (
                                                !Services.GetMutes(Client, corradeConfiguration.ServicesTimeout,
                                                    ref mutes))
                                                throw new ScriptException(ScriptError.COULD_NOT_RETRIEVE_MUTE_LIST);
                                            break;
                                        default:
                                            mutes = Cache.MuteCache.AsEnumerable();
                                            break;
                                    }
                                    lock (GroupSoftBansLock)
                                    {
                                        switch (!GroupSoftBans.ContainsKey(groupUUID))
                                        {
                                            case true:
                                                GroupSoftBans.Add(groupUUID,
                                                    new Collections.ObservableHashSet<UUID>());
                                                GroupSoftBans[groupUUID].CollectionChanged += HandleGroupSoftBansChanged;
                                                GroupSoftBans[groupUUID].UnionWith(
                                                    mutes.AsParallel()
                                                        .Where(o => o.Type.Equals(MuteType.Resident))
                                                        .Select(o => o.ID));
                                                break;
                                            default:
                                                GroupSoftBans[groupUUID].UnionWith(
                                                    mutes.AsParallel()
                                                        .Where(o => o.Type.Equals(MuteType.Resident))
                                                        .Select(o => o.ID));
                                                break;
                                        }
                                    }
                                    SaveGroupSoftBansState.Invoke();
                                    break;
                                default:
                                    throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                            }
                            break;
                        case Action.EXPORT:
                            switch (Reflection.GetEnumValueFromName<Entity>(
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY)),
                                        corradeCommandParameters.Message))
                                    .ToLowerInvariant()))
                            {
                                case Entity.MUTE:
                                    if (
                                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                            (int) Configuration.Permissions.Mute))
                                    {
                                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                                    }
                                    Collections.ObservableHashSet<UUID> softBans;
                                    lock (GroupSoftBansLock)
                                    {
                                        if (!GroupSoftBans.TryGetValue(groupUUID, out softBans))
                                            break;
                                    }
                                    var mutes = Enumerable.Empty<MuteEntry>();
                                    // retrieve the current mute list
                                    switch (Cache.MuteCache.IsVirgin)
                                    {
                                        case true:
                                            if (
                                                !Services.GetMutes(Client, corradeConfiguration.ServicesTimeout,
                                                    ref mutes))
                                                throw new ScriptException(ScriptError.COULD_NOT_RETRIEVE_MUTE_LIST);
                                            break;
                                        default:
                                            mutes = Cache.MuteCache.AsEnumerable();
                                            break;
                                    }
                                    // Get the mute flags - default is "Default" equivalent to 0
                                    var muteFlags = MuteFlags.Default;
                                    CSV.ToEnumerable(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FLAGS)),
                                                corradeCommandParameters.Message)))
                                        .ToArray()
                                        .AsParallel()
                                        .Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                                            typeof (MuteFlags).GetFields(BindingFlags.Public |
                                                                         BindingFlags.Static)
                                                .AsParallel()
                                                .Where(p => Strings.Equals(o, p.Name, StringComparison.Ordinal))
                                                .ForAll(
                                                    q =>
                                                    {
                                                        BitTwiddling.SetMaskFlag(ref muteFlags,
                                                            (MuteFlags) q.GetValue(null));
                                                    }));
                                    var MuteListUpdatedEvent = new ManualResetEvent(false);
                                    EventHandler<EventArgs> MuteListUpdatedEventHandler =
                                        (sender, args) => MuteListUpdatedEvent.Set();
                                    softBans.AsParallel().ForAll(o =>
                                    {
                                        // check that the mute entry does not already exist
                                        if (
                                            mutes.ToList()
                                                .AsParallel()
                                                .Any(p => p.ID.Equals(o) && p.Type.Equals(MuteType.Resident)))
                                            return;
                                        var agentName = string.Empty;
                                        if (
                                            !Resolvers.AgentUUIDToName(Client, o,
                                                corradeConfiguration.ServicesTimeout,
                                                ref agentName))
                                            return;
                                        lock (Locks.ClientInstanceSelfLock)
                                        {
                                            // add mute
                                            Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                                            Client.Self.UpdateMuteListEntry(MuteType.Resident, o, agentName, muteFlags);
                                            if (
                                                !MuteListUpdatedEvent.WaitOne(
                                                    (int) corradeConfiguration.ServicesTimeout, false))
                                            {
                                                Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                                return;
                                            }
                                            Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                        }
                                        // add the mute to the cache
                                        Cache.AddMute(muteFlags, o, agentName, MuteType.Resident);
                                    });
                                    break;
                                case Entity.GROUP:
                                    Dictionary<UUID, DateTime> bannedAgents = null;
                                    if (
                                        !Services.GetGroupBans(Client, groupUUID, corradeConfiguration.ServicesTimeout,
                                            ref bannedAgents))
                                    {
                                        throw new ScriptException(ScriptError.COULD_NOT_RETRIEVE_GROUP_BAN_LIST);
                                    }
                                    if (bannedAgents == null)
                                        throw new ScriptException(ScriptError.COULD_NOT_RETRIEVE_GROUP_BAN_LIST);
                                    Collections.ObservableHashSet<UUID> groupSoftBans;
                                    lock (GroupSoftBansLock)
                                    {
                                        if (!GroupSoftBans.ContainsKey(groupUUID) ||
                                            !GroupSoftBans.TryGetValue(groupUUID, out groupSoftBans))
                                            break;
                                    }
                                    groupSoftBans.RemoveWhere(o => bannedAgents.ContainsKey(o));
                                    // check whether added bans would not exceed the maximum ban list in Second Life
                                    if (action.Equals(Action.BAN) && Helpers.IsSecondLife(Client) &&
                                        bannedAgents.Count + groupSoftBans.Count > Constants.GROUPS.MAXIMUM_GROUP_BANS)
                                        throw new ScriptException(ScriptError.BAN_WOULD_EXCEED_MAXIMUM_BAN_LIST_LENGTH);
                                    // ban the avatars
                                    lock (Locks.ClientInstanceGroupsLock)
                                    {
                                        var GroupBanEvent = new ManualResetEvent(false);
                                        Client.Groups.RequestBanAction(groupUUID,
                                            GroupBanAction.Ban,
                                            groupSoftBans.ToArray(), (sender, args) => { GroupBanEvent.Set(); });
                                        if (!GroupBanEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                        {
                                            throw new ScriptException(ScriptError.TIMEOUT_MODIFYING_GROUP_BAN_LIST);
                                        }
                                    }
                                    break;
                                default:
                                    throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}