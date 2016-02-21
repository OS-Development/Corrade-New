///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getrolesmembers =
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
                    HashSet<KeyValuePair<UUID, UUID>> groupRolesMembers = null;
                    ManualResetEvent GroupRoleMembersReplyEvent = new ManualResetEvent(false);
                    EventHandler<GroupRolesMembersReplyEventArgs> GroupRolesMembersEventHandler =
                        (sender, args) =>
                        {
                            groupRolesMembers = new HashSet<KeyValuePair<UUID, UUID>>(args.RolesMembers);
                            GroupRoleMembersReplyEvent.Set();
                        };
                    lock (Locks.ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupRoleMembersReply += GroupRolesMembersEventHandler;
                        Client.Groups.RequestGroupRolesMembers(corradeCommandParameters.Group.UUID);
                        if (!GroupRoleMembersReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_GETING_GROUP_ROLES_MEMBERS);
                        }
                        Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                    }
                    // First resolve the all the role names to role UUIDs
                    Hashtable roleUUIDNames = new Hashtable(groupRolesMembers.Count);
                    object LockObject = new object();
                    Parallel.ForEach(groupRolesMembers.AsParallel().GroupBy(o => o.Key).Select(o => o.First().Key),
                        o =>
                        {
                            string roleName = string.Empty;
                            switch (Resolvers.RoleUUIDToName(Client, o, corradeCommandParameters.Group.UUID,
                                corradeConfiguration.ServicesTimeout,
                                ref roleName))
                            {
                                case true:
                                    lock (LockObject)
                                    {
                                        roleUUIDNames.Add(o, roleName);
                                    }
                                    break;
                            }
                        });
                    // Next, associate role names with agent names and UUIDs.
                    List<string> csv = new List<string>();
                    Parallel.ForEach(groupRolesMembers.AsParallel().Where(o => roleUUIDNames.ContainsKey(o.Key)), o =>
                    {
                        string agentName = string.Empty;
                        if (Resolvers.AgentUUIDToName(Client, o.Value, corradeConfiguration.ServicesTimeout,
                            ref agentName))
                        {
                            lock (LockObject)
                            {
                                csv.Add(roleUUIDNames[o.Key] as string);
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