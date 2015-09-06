///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> getrolemembers =
                (commandGroup, message, result) =>
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
                    if (!currentGroups.ToList().Any(o => o.Equals(commandGroup.UUID)))
                    {
                        throw new ScriptException(ScriptError.NOT_IN_GROUP);
                    }
                    string role =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ROLE)),
                            message));
                    if (string.IsNullOrEmpty(role))
                    {
                        throw new ScriptException(ScriptError.NO_ROLE_NAME_SPECIFIED);
                    }
                    UUID roleUUID;
                    if (!UUID.TryParse(role, out roleUUID) && !RoleNameToUUID(role, commandGroup.UUID,
                        corradeConfiguration.ServicesTimeout,
                        ref roleUUID))
                    {
                        throw new ScriptException(ScriptError.ROLE_NOT_FOUND);
                    }
                    List<string> csv = new List<string>();
                    // get all roles and members
                    HashSet<KeyValuePair<UUID, UUID>> groupRolesMembers = new HashSet<KeyValuePair<UUID, UUID>>();
                    ManualResetEvent GroupRoleMembersReplyEvent = new ManualResetEvent(false);
                    EventHandler<GroupRolesMembersReplyEventArgs> GroupRolesMembersEventHandler =
                        (sender, args) =>
                        {
                            groupRolesMembers = new HashSet<KeyValuePair<UUID, UUID>>(args.RolesMembers);
                            GroupRoleMembersReplyEvent.Set();
                        };
                    lock (ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupRoleMembersReply += GroupRolesMembersEventHandler;
                        Client.Groups.RequestGroupRolesMembers(commandGroup.UUID);
                        if (!GroupRoleMembersReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_GETING_GROUP_ROLES_MEMBERS);
                        }
                        Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                    }
                    foreach (
                        KeyValuePair<UUID, UUID> pair in
                            groupRolesMembers.AsParallel().Where(o => o.Key.Equals(roleUUID)))
                    {
                        string agentName = string.Empty;
                        switch (!AgentUUIDToName(pair.Value, corradeConfiguration.ServicesTimeout, ref agentName))
                        {
                            case true:
                                continue;
                            default:
                                csv.Add(agentName);
                                csv.Add(pair.Value.ToString());
                                break;
                        }
                    }
                    if (csv.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(csv));
                    }
                };
        }
    }
}