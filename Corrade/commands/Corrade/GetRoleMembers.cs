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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getrolemembers =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    UUID groupUUID;
                    string target = wasInput(
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
                    IEnumerable<UUID> currentGroups = Enumerable.Empty<UUID>();
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
                    string role =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ROLE)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(role))
                    {
                        throw new ScriptException(ScriptError.NO_ROLE_NAME_SPECIFIED);
                    }
                    UUID roleUUID;
                    if (!UUID.TryParse(role, out roleUUID) &&
                        !Resolvers.RoleNameToUUID(Client, role, groupUUID,
                            corradeConfiguration.ServicesTimeout,
                            ref roleUUID))
                    {
                        throw new ScriptException(ScriptError.ROLE_NOT_FOUND);
                    }
                    // get all roles and members
                    HashSet<KeyValuePair<UUID, UUID>> groupRolesMembers = new HashSet<KeyValuePair<UUID, UUID>>();
                    ManualResetEvent GroupRoleMembersReplyEvent = new ManualResetEvent(false);
                    EventHandler<GroupRolesMembersReplyEventArgs> GroupRolesMembersEventHandler =
                        (sender, args) =>
                        {
                            groupRolesMembers.UnionWith(args.RolesMembers);
                            GroupRoleMembersReplyEvent.Set();
                        };
                    lock (Locks.ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupRoleMembersReply += GroupRolesMembersEventHandler;
                        Client.Groups.RequestGroupRolesMembers(groupUUID);
                        if (!GroupRoleMembersReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_GETING_GROUP_ROLES_MEMBERS);
                        }
                        Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                    }
                    List<string> csv = new List<string>();
                    object LockObject = new object();
                    groupRolesMembers.AsParallel().Where(o => o.Key.Equals(roleUUID)).ForAll(o =>
                    {
                        string agentName = string.Empty;
                        if (Resolvers.AgentUUIDToName(Client, o.Value, corradeConfiguration.ServicesTimeout,
                            ref agentName))
                        {
                            lock (LockObject)
                            {
                                csv.Add(agentName);
                                csv.Add(o.Value.ToString());
                            }
                        }
                    });
                    if (csv.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                    }
                };
        }
    }
}