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
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> deleterole =
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
                    if (!new HashSet<UUID>(currentGroups).Contains(commandGroup.UUID))
                    {
                        throw new ScriptException(ScriptError.NOT_IN_GROUP);
                    }
                    if (
                        !HasGroupPowers(Client.Self.AgentID, commandGroup.UUID, GroupPowers.DeleteRole,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout) ||
                        !HasGroupPowers(Client.Self.AgentID, commandGroup.UUID, GroupPowers.RemoveMember,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    string role =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ROLE)),
                            message));
                    UUID roleUUID;
                    if (!UUID.TryParse(role, out roleUUID) && !RoleNameToUUID(role, commandGroup.UUID,
                        corradeConfiguration.ServicesTimeout,
                        ref roleUUID))
                    {
                        throw new ScriptException(ScriptError.ROLE_NOT_FOUND);
                    }
                    if (roleUUID.Equals(UUID.Zero))
                    {
                        throw new ScriptException(ScriptError.CANNOT_DELETE_THE_EVERYONE_ROLE);
                    }
                    OpenMetaverse.Group targetGroup = new OpenMetaverse.Group();
                    if (!RequestGroup(commandGroup.UUID, corradeConfiguration.ServicesTimeout, ref targetGroup))
                    {
                        throw new ScriptException(ScriptError.GROUP_NOT_FOUND);
                    }
                    if (targetGroup.OwnerRole.Equals(roleUUID))
                    {
                        throw new ScriptException(ScriptError.CANNOT_REMOVE_OWNER_ROLE);
                    }
                    // remove members from role
                    ManualResetEvent GroupRoleMembersReplyEvent = new ManualResetEvent(false);
                    EventHandler<GroupRolesMembersReplyEventArgs> GroupRolesMembersEventHandler = (sender, args) =>
                    {
                        Parallel.ForEach(args.RolesMembers.AsParallel().Where(o => o.Key.Equals(roleUUID)),
                            o => Client.Groups.RemoveFromRole(commandGroup.UUID, roleUUID, o.Value));
                        GroupRoleMembersReplyEvent.Set();
                    };
                    lock (ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupRoleMembersReply += GroupRolesMembersEventHandler;
                        Client.Groups.RequestGroupRolesMembers(commandGroup.UUID);
                        if (!GroupRoleMembersReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_EJECTING_AGENT);
                        }
                        Client.Groups.GroupRoleMembersReply -= GroupRolesMembersEventHandler;
                    }
                    Client.Groups.DeleteRole(commandGroup.UUID, roleUUID);
                };
        }
    }
}