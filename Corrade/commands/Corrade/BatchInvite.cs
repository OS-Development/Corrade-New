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
            public static Action<Group, string, Dictionary<string, string>> batchinvite =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    if (
                        !HasGroupPowers(Client.Self.AgentID, commandGroup.UUID, GroupPowers.Invite,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    // Get the roles to invite to.
                    HashSet<UUID> roleUUIDs = new HashSet<UUID>();
                    foreach (
                        string role in
                            wasCSVToEnumerable(
                                wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ROLE)),
                                    message)))
                                .AsParallel().Where(o => !string.IsNullOrEmpty(o)))
                    {
                        UUID roleUUID;
                        if (!UUID.TryParse(role, out roleUUID) &&
                            !RoleNameToUUID(role, commandGroup.UUID,
                                corradeConfiguration.ServicesTimeout, ref roleUUID))
                        {
                            throw new ScriptException(ScriptError.ROLE_NOT_FOUND);
                        }
                        if (!roleUUIDs.Contains(roleUUID))
                        {
                            roleUUIDs.Add(roleUUID);
                        }
                    }
                    // No roles specified, so assume everyone role.
                    if (!roleUUIDs.Any())
                    {
                        roleUUIDs.Add(UUID.Zero);
                    }
                    if (!roleUUIDs.All(o => o.Equals(UUID.Zero)) &&
                        !HasGroupPowers(Client.Self.AgentID, commandGroup.UUID, GroupPowers.AssignMember,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                    {
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
                    lock (ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupMembersReply += HandleGroupMembersReplyDelegate;
                        Client.Groups.RequestGroupMembers(commandGroup.UUID);
                        if (!groupMembersReceivedEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_GROUP_MEMBERS);
                        }
                        Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                    }
                    HashSet<string> data = new HashSet<string>();
                    object LockObject = new object();
                    Parallel.ForEach(
                        wasCSVToEnumerable(
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.AVATARS)),
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
                                            Client.Groups.Invite(commandGroup.UUID, roleUUIDs.ToList(),
                                                agentUUID);
                                            break;
                                    }
                                });
                    if (data.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(data));
                    }
                };
        }
    }
}