///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> softban =
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

                    // Get current group bans.
                    Dictionary<UUID, DateTime> bannedAgents = null;
                    if (
                        !Services.GetGroupBans(Client, groupUUID, corradeConfiguration.ServicesTimeout,
                            ref bannedAgents))
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.COULD_NOT_RETRIEVE_GROUP_BAN_LIST);
                    if (bannedAgents == null)
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.COULD_NOT_RETRIEVE_GROUP_BAN_LIST);

                    var action = Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                    );
                    var succeeded = false;
                    var LockObject = new object();
                    var softBans = new List<SoftBan>();

                    KeyValuePair<UUID, string>[] avatars = null;
                    var times = new List<string>(CSV.ToEnumerable(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TIME)),
                                corradeCommandParameters.Message))));
                    var data = new HashSet<string>();

                    switch (action)
                    {
                        case Enumerations.Action.BAN:
                        case Enumerations.Action.UNBAN:
                        case Enumerations.Action.SCHEDULE:
                            var AvatarsLock = new object();
                            var csvAvatars = new List<string>(CSV.ToEnumerable(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AVATARS)),
                                        corradeCommandParameters.Message))));
                            avatars = new KeyValuePair<UUID, string>[csvAvatars.Count];
                            csvAvatars
                                .AsParallel().Select((input, index) => new {input, index}).ForAll(o =>
                                {
                                    if (string.IsNullOrEmpty(o.input))
                                        return;

                                    UUID agentUUID;
                                    if (!UUID.TryParse(o.input, out agentUUID))
                                    {
                                        var fullName =
                                            new List<string>(wasOpenMetaverse.Helpers.GetAvatarNames(o.input));
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
                                                if (!data.Contains(o.input))
                                                    data.Add(o.input);
                                            }
                                            return;
                                        }
                                    }

                                    lock (AvatarsLock)
                                    {
                                        if (!avatars.Any(p => p.Key.Equals(agentUUID)))
                                            avatars[o.index] = new KeyValuePair<UUID, string>(agentUUID, o.input);
                                    }
                                });

                            if (!avatars.Any())
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_AVATARS_FOUND);
                            break;
                    }

                    switch (action)
                    {
                        case Enumerations.Action.BAN:
                        case Enumerations.Action.UNBAN:
                            // ban or unban the avatars
                            var groupSoftBansModified = false;
                            switch (action)
                            {
                                case Enumerations.Action.BAN:
                                    var notes = new List<string>(CSV.ToEnumerable(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NOTE)),
                                                corradeCommandParameters.Message))));
                                    avatars.AsParallel().Select((input, index) => new {input, index}).ForAll(o =>
                                    {
                                        // Get the note.
                                        var note = notes.ElementAtOrDefault(o.index);

                                        // Get the hard time.
                                        ulong banTime;
                                        if (
                                            !ulong.TryParse(times.ElementAtOrDefault(o.index), NumberStyles.Float,
                                                Utils.EnUsCulture, out banTime))
                                            banTime = 0;

                                        var fullName =
                                            new List<string>(wasOpenMetaverse.Helpers.GetAvatarNames(o.input.Value));
                                        if (!fullName.Any())
                                            return;

                                        var softBan = new SoftBan
                                        {
                                            Agent = o.input.Key,
                                            FirstName = fullName.First(),
                                            LastName = fullName.Last(),
                                            Note = note,
                                            Time = banTime,
                                            Timestamp = DateTime.UtcNow.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP),
                                            Last = DateTime.UtcNow.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP)
                                        };

                                        lock (GroupSoftBansLock)
                                        {
                                            switch (GroupSoftBans.ContainsKey(groupUUID))
                                            {
                                                case true:
                                                    if (
                                                        GroupSoftBans[groupUUID].AsParallel()
                                                            .Any(p => p.Agent.Equals(o.input.Key)))
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
                                    avatars.AsParallel().ForAll(o =>
                                    {
                                        lock (GroupSoftBansLock)
                                        {
                                            switch (GroupSoftBans.ContainsKey(groupUUID))
                                            {
                                                case true:
                                                    if (
                                                        !GroupSoftBans[groupUUID].AsParallel()
                                                            .Any(p => p.Agent.Equals(o.Key)))
                                                        return;
                                                    GroupSoftBans[groupUUID].RemoveWhere(p => p.Agent.Equals(o.Key));
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

                            var process = avatars
                                .GroupBy(o => o.Key)
                                .ToDictionary(o => o.Key, o => o.FirstOrDefault().Value);

                            switch (action)
                            {
                                case Enumerations.Action.UNBAN: // If this is an unban request, unban all the agents.
                                    process.AsParallel()
                                        .Select(o => o.Key)
                                        .Where(o => bannedAgents.ContainsKey(o))
                                        .ForAll(o =>
                                        {
                                            var GroupBanEvent = new ManualResetEventSlim(false);
                                            Client.Groups.RequestBanAction(groupUUID, GroupBanAction.Unban,
                                                new[] {o}, (sender, args) => { GroupBanEvent.Set(); });
                                            if (
                                                !GroupBanEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                                                lock (LockObject)
                                                {
                                                    if (!data.Contains(process[o]))
                                                        data.Add(process[o]);
                                                }
                                        });
                                    break;

                                case Enumerations.Action.BAN: // If this a ban request, ban, demote and eject.
                                    // By default a soft-ban also ejects an agent.
                                    /*bool alsoEject;
                                    if (!bool.TryParse(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.EJECT)),
                                                corradeCommandParameters.Message)),
                                        out alsoEject) || alsoEject == false) break;*/
                                    // Get the group members.
                                    Dictionary<UUID, GroupMember> groupMembers = null;
                                    var groupMembersReceivedEvent = new ManualResetEventSlim(false);
                                    var groupMembersRequestUUID = UUID.Zero;
                                    EventHandler<GroupMembersReplyEventArgs> HandleGroupMembersReplyDelegate =
                                        (sender, args) =>
                                        {
                                            if (!groupMembersRequestUUID.Equals(args.RequestID) ||
                                                !args.GroupID.Equals(groupUUID))
                                                return;

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
                                    var groupRolesMembers = new HashSet<KeyValuePair<UUID, UUID>>();
                                    var GroupRoleMembersReplyEvent = new ManualResetEventSlim(false);
                                    var requestUUID = UUID.Zero;
                                    EventHandler<GroupRolesMembersReplyEventArgs> GroupRoleMembersEventHandler =
                                        (sender, args) =>
                                        {
                                            if (!requestUUID.Equals(args.RequestID) || !args.GroupID.Equals(groupUUID))
                                                return;
                                            groupRolesMembers.UnionWith(args.RolesMembers);
                                            GroupRoleMembersReplyEvent.Set();
                                        };
                                    Client.Groups.GroupRoleMembersReply += GroupRoleMembersEventHandler;
                                    requestUUID = Client.Groups.RequestGroupRolesMembers(groupUUID);
                                    if (
                                        !GroupRoleMembersReplyEvent.Wait(
                                            (int) corradeConfiguration.ServicesTimeout))
                                    {
                                        Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.TIMEOUT_GETTING_GROUP_ROLE_MEMBERS);
                                    }
                                    Client.Groups.GroupRoleMembersReply -= GroupRoleMembersEventHandler;
                                    // Retrieve the soft ban list for the group.
                                    ObservableHashSet<SoftBan> groupSoftBans;
                                    lock (GroupSoftBansLock)
                                    {
                                        if (!GroupSoftBans.ContainsKey(groupUUID) ||
                                            !GroupSoftBans.TryGetValue(groupUUID, out groupSoftBans))
                                            break;
                                    }
                                    groupMembers
                                        .AsParallel()
                                        .Where(o => process.ContainsKey(o.Value.ID))
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
                                                        if (!data.Contains(process[o.Value.ID]))
                                                            data.Add(process[o.Value.ID]);
                                                    }
                                                    return;
                                                }

                                                // Ban them.
                                                var softBan =
                                                    groupSoftBans.AsParallel()
                                                        .FirstOrDefault(p => p.Agent.Equals(o.Value.ID));
                                                // No softban found, so skip.
                                                if (softBan.Equals(default(SoftBan)))
                                                {
                                                    lock (LockObject)
                                                    {
                                                        if (!data.Contains(process[o.Value.ID]))
                                                            data.Add(process[o.Value.ID]);
                                                    }
                                                    return;
                                                }
                                                // No hard time requested so no need to ban.
                                                if (!softBan.Time.Equals(0))
                                                {
                                                    // Check whether an agent has already been banned, whether there are no soft bans
                                                    // for the agent or whether this is Second Life and we would exceed the ban list.
                                                    if (bannedAgents.ContainsKey(o.Value.ID) ||
                                                        wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                                                        bannedAgents.Count + 1 >
                                                        wasOpenMetaverse.Constants.GROUPS.MAXIMUM_GROUP_BANS)
                                                    {
                                                        lock (LockObject)
                                                        {
                                                            if (!data.Contains(process[o.Value.ID]))
                                                                data.Add(process[o.Value.ID]);
                                                        }
                                                        return;
                                                    }
                                                    // Now ban the agent.
                                                    var GroupBanEvent = new ManualResetEventSlim(false);
                                                    Client.Groups.RequestBanAction(groupUUID,
                                                        GroupBanAction.Ban, new[] {o.Value.ID},
                                                        (sender, args) => { GroupBanEvent.Set(); });
                                                    if (
                                                        !GroupBanEvent.Wait(
                                                            (int) corradeConfiguration.ServicesTimeout))
                                                    {
                                                        lock (LockObject)
                                                        {
                                                            if (!data.Contains(process[o.Value.ID]))
                                                                data.Add(process[o.Value.ID]);
                                                        }
                                                        return;
                                                    }
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
                                                Client.Groups.EjectUser(groupUUID, o.Value.ID);
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
                                                            if (!data.Contains(process[o.Value.ID]))
                                                                data.Add(process[o.Value.ID]);
                                                        }
                                                        return;
                                                }
                                            });
                                    break;
                            }
                            if (data.Any())
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                    CSV.FromEnumerable(data));
                            break;

                        case Enumerations.Action.LIST:
                            var csv = new List<string>();
                            lock (GroupSoftBansLock)
                            {
                                if (GroupSoftBans.ContainsKey(groupUUID))
                                    GroupSoftBans[groupUUID].AsParallel().ForAll(o =>
                                    {
                                        lock (LockObject)
                                        {
                                            csv.AddRange(new[]
                                            {
                                                Reflection.GetStructureMemberName(o, o.FirstName),
                                                o.Agent.ToString()
                                            });
                                            csv.AddRange(new[]
                                            {
                                                Reflection.GetStructureMemberName(o, o.FirstName),
                                                o.FirstName
                                            });
                                            csv.AddRange(new[]
                                            {
                                                Reflection.GetStructureMemberName(o, o.LastName),
                                                o.LastName
                                            });
                                            csv.AddRange(new[]
                                            {
                                                Reflection.GetStructureMemberName(o, o.Time),
                                                o.Time.ToString()
                                            });
                                            csv.AddRange(new[]
                                            {
                                                Reflection.GetStructureMemberName(o, o.Note),
                                                o.Note
                                            });
                                            csv.AddRange(new[]
                                            {
                                                Reflection.GetStructureMemberName(o, o.Timestamp),
                                                o.Timestamp
                                            });
                                        }
                                    });
                            }
                            if (csv.Any())
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                    CSV.FromEnumerable(csv));
                            break;

                        case Enumerations.Action.SCHEDULE:
                            avatars.AsParallel().Select((input, index) => new {input, index}).ForAll(o =>
                            {
                                // Get the hard time.
                                ulong banTime;
                                if (
                                    !ulong.TryParse(times.ElementAtOrDefault(o.index), NumberStyles.Float,
                                        Utils.EnUsCulture, out banTime))
                                    return;

                                ObservableHashSet<SoftBan> groupSoftBans;
                                lock (GroupSoftBansLock)
                                {
                                    if (!GroupSoftBans.ContainsKey(groupUUID) ||
                                        !GroupSoftBans.TryGetValue(groupUUID, out groupSoftBans))
                                        return;
                                }

                                var softBan = groupSoftBans.FirstOrDefault(p => p.Agent.Equals(o.input.Key));
                                if (softBan.Equals(default(SoftBan)))
                                    return;

                                // Set the new hard ban time.
                                softBan.Time = banTime;

                                lock (GroupSoftBansLock)
                                {
                                    GroupSoftBans[groupUUID].RemoveWhere(p => p.Agent.Equals(o.input.Key));
                                    GroupSoftBans[groupUUID].Add(softBan);
                                }
                            });
                            break;

                        case Enumerations.Action.IMPORT:
                            switch (Reflection.GetEnumValueFromName<Enumerations.Entity>(
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                        corradeCommandParameters.Message))
                            ))
                            {
                                case Enumerations.Entity.GROUP:
                                    // Generate the softban list.
                                    bannedAgents.AsParallel().ForAll(o =>
                                    {
                                        var agentName = string.Empty;
                                        if (
                                            !Resolvers.AgentUUIDToName(Client, o.Key,
                                                corradeConfiguration.ServicesTimeout, ref agentName))
                                            return;

                                        var fullName = wasOpenMetaverse.Helpers.GetAvatarNames(agentName);
                                        var enumerable = fullName as IList<string> ?? fullName.ToList();
                                        if (!enumerable.Any())
                                            return;

                                        lock (LockObject)
                                        {
                                            softBans.Add(new SoftBan
                                            {
                                                Agent = o.Key,
                                                FirstName = enumerable.First(),
                                                LastName = enumerable.Last(),
                                                Timestamp = o.Value.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP),
                                                Last = o.Value.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP)
                                            });
                                        }
                                    });
                                    lock (GroupSoftBansLock)
                                    {
                                        switch (!GroupSoftBans.ContainsKey(groupUUID))
                                        {
                                            case true:
                                                GroupSoftBans.Add(groupUUID,
                                                    new ObservableHashSet<SoftBan>());
                                                GroupSoftBans[groupUUID].CollectionChanged +=
                                                    HandleGroupSoftBansChanged;
                                                GroupSoftBans[groupUUID].UnionWith(softBans);
                                                break;

                                            default:
                                                GroupSoftBans[groupUUID].UnionWith(softBans);
                                                break;
                                        }
                                    }
                                    SaveGroupSoftBansState.Invoke();
                                    break;

                                case Enumerations.Entity.MUTE:
                                    if (
                                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                            (int) Configuration.Permissions.Mute))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                                    var mutes = Enumerable.Empty<MuteEntry>();
                                    // retrieve the current mute list
                                    switch (Cache.MuteCache.IsVirgin)
                                    {
                                        case true:
                                            if (
                                                !Services.GetMutes(Client, corradeConfiguration.ServicesTimeout,
                                                    ref mutes))
                                                throw new Command.ScriptException(
                                                    Enumerations.ScriptError.COULD_NOT_RETRIEVE_MUTE_LIST);
                                            break;

                                        default:
                                            mutes = Cache.MuteCache.OfType<MuteEntry>();
                                            break;
                                    }
                                    // Generate the softban list.
                                    mutes.AsParallel()
                                        .Where(o => o.Type.Equals(MuteType.Resident)).ForAll(o =>
                                        {
                                            var agentName = string.Empty;
                                            if (
                                                !Resolvers.AgentUUIDToName(Client, o.ID,
                                                    corradeConfiguration.ServicesTimeout, ref agentName))
                                                return;
                                            var fullName = wasOpenMetaverse.Helpers.GetAvatarNames(agentName);
                                            var enumerable = fullName as IList<string> ?? fullName.ToList();
                                            if (!enumerable.Any())
                                                return;
                                            lock (LockObject)
                                            {
                                                softBans.Add(new SoftBan
                                                {
                                                    Agent = o.ID,
                                                    FirstName = enumerable.First(),
                                                    LastName = enumerable.Last(),
                                                    Timestamp =
                                                        DateTime.UtcNow.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP),
                                                    Last = DateTime.UtcNow.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP)
                                                });
                                            }
                                        });
                                    lock (GroupSoftBansLock)
                                    {
                                        switch (!GroupSoftBans.ContainsKey(groupUUID))
                                        {
                                            case true:
                                                GroupSoftBans.Add(groupUUID,
                                                    new ObservableHashSet<SoftBan>());
                                                GroupSoftBans[groupUUID].CollectionChanged +=
                                                    HandleGroupSoftBansChanged;
                                                GroupSoftBans[groupUUID].UnionWith(softBans);
                                                break;

                                            default:
                                                GroupSoftBans[groupUUID].UnionWith(softBans);
                                                break;
                                        }
                                    }
                                    SaveGroupSoftBansState.Invoke();
                                    break;

                                default:
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                            }
                            break;

                        case Enumerations.Action.EXPORT:
                            switch (Reflection.GetEnumValueFromName<Enumerations.Entity>(
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                        corradeCommandParameters.Message))
                            ))
                            {
                                case Enumerations.Entity.MUTE:
                                    if (
                                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                            (int) Configuration.Permissions.Mute))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                                    ObservableHashSet<SoftBan> muteSoftBans;
                                    lock (GroupSoftBansLock)
                                    {
                                        if (!GroupSoftBans.TryGetValue(groupUUID, out muteSoftBans))
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
                                                throw new Command.ScriptException(
                                                    Enumerations.ScriptError.COULD_NOT_RETRIEVE_MUTE_LIST);
                                            break;

                                        default:
                                            mutes = Cache.MuteCache.OfType<MuteEntry>();
                                            break;
                                    }
                                    // Get the mute flags - default is "Default" equivalent to 0
                                    var muteFlags = MuteFlags.Default;
                                    CSV.ToEnumerable(
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                                        .FLAGS)),
                                                    corradeCommandParameters.Message)))
                                        .AsParallel()
                                        .Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                                            typeof(MuteFlags).GetFields(BindingFlags.Public |
                                                                        BindingFlags.Static)
                                                .AsParallel()
                                                .Where(p => string.Equals(o, p.Name, StringComparison.Ordinal))
                                                .ForAll(
                                                    q =>
                                                    {
                                                        BitTwiddling.SetMaskFlag(ref muteFlags,
                                                            (MuteFlags) q.GetValue(null));
                                                    }));
                                    muteSoftBans.AsParallel().ForAll(o =>
                                    {
                                        // check that the mute entry does not already exist
                                        if (
                                            mutes.ToList()
                                                .AsParallel()
                                                .Any(p => p.ID.Equals(o.Agent) && p.Type.Equals(MuteType.Resident)))
                                            return;
                                        var MuteListUpdatedEvent = new ManualResetEventSlim(false);
                                        EventHandler<EventArgs> MuteListUpdatedEventHandler =
                                            (sender, args) => MuteListUpdatedEvent.Set();

                                        var agentName = string.Empty;
                                        if (
                                            !Resolvers.AgentUUIDToName(Client, o.Agent,
                                                corradeConfiguration.ServicesTimeout, ref agentName))
                                            return;

                                        Locks.ClientInstanceSelfLock.EnterWriteLock();
                                        // add mute
                                        Client.Self.MuteListUpdated += MuteListUpdatedEventHandler;
                                        Client.Self.UpdateMuteListEntry(MuteType.Resident, o.Agent, agentName,
                                            muteFlags);
                                        if (
                                            !MuteListUpdatedEvent.Wait(
                                                (int) corradeConfiguration.ServicesTimeout))
                                        {
                                            Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                                            return;
                                        }
                                        Client.Self.MuteListUpdated -= MuteListUpdatedEventHandler;
                                        Locks.ClientInstanceSelfLock.ExitWriteLock();
                                        // add the mute to the cache
                                        Cache.AddMute(muteFlags, o.Agent, agentName, MuteType.Resident);
                                    });
                                    break;

                                case Enumerations.Entity.GROUP:
                                    // Retrieve the soft ban list for the group.
                                    ObservableHashSet<SoftBan> groupSoftBans;
                                    lock (GroupSoftBansLock)
                                    {
                                        if (!GroupSoftBans.ContainsKey(groupUUID) ||
                                            !GroupSoftBans.TryGetValue(groupUUID, out groupSoftBans))
                                            break;
                                    }
                                    groupSoftBans.RemoveWhere(o => bannedAgents.ContainsKey(o.Agent));
                                    // check whether added bans would not exceed the maximum ban list in Second Life
                                    if (action.Equals(Enumerations.Action.BAN) &&
                                        wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                                        bannedAgents.Count + groupSoftBans.Count >
                                        wasOpenMetaverse.Constants.GROUPS.MAXIMUM_GROUP_BANS)
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.BAN_WOULD_EXCEED_MAXIMUM_BAN_LIST_LENGTH);
                                    // ban the avatars
                                    var GroupBanEvent = new ManualResetEventSlim(false);
                                    Client.Groups.RequestBanAction(groupUUID,
                                        GroupBanAction.Ban,
                                        groupSoftBans.Select(o => o.Agent).ToArray(),
                                        (sender, args) => { GroupBanEvent.Set(); });
                                    if (!GroupBanEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.TIMEOUT_MODIFYING_GROUP_BAN_LIST);
                                    break;

                                default:
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                            }
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}