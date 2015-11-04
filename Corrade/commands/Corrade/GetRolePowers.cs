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
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getrolepowers =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name, (int) Configuration.Permissions.Group))
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
                    if (!new HashSet<UUID>(currentGroups).Contains(corradeCommandParameters.Group.UUID))
                    {
                        throw new ScriptException(ScriptError.NOT_IN_GROUP);
                    }
                    if (
                        !HasGroupPowers(Client.Self.AgentID, corradeCommandParameters.Group.UUID,
                            GroupPowers.RoleProperties,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    string role =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ROLE)),
                            corradeCommandParameters.Message));
                    UUID roleUUID;
                    if (!UUID.TryParse(role, out roleUUID) && !RoleNameToUUID(role, corradeCommandParameters.Group.UUID,
                        corradeConfiguration.ServicesTimeout,
                        ref roleUUID))
                    {
                        throw new ScriptException(ScriptError.ROLE_NOT_FOUND);
                    }
                    HashSet<string> data = new HashSet<string>();
                    ManualResetEvent GroupRoleDataReplyEvent = new ManualResetEvent(false);
                    EventHandler<GroupRolesDataReplyEventArgs> GroupRoleDataEventHandler = (sender, args) =>
                    {
                        GroupRole queryRole =
                            args.Roles.Values.AsParallel().FirstOrDefault(o => o.ID.Equals(roleUUID));
                        data.UnionWith(typeof (GroupPowers).GetFields(BindingFlags.Public | BindingFlags.Static)
                            .AsParallel().Where(
                                o =>
                                    !(((ulong) o.GetValue(null) &
                                       (ulong) queryRole.Powers)).Equals(0))
                            .Select(o => o.Name));
                        GroupRoleDataReplyEvent.Set();
                    };
                    lock (ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupRoleDataReply += GroupRoleDataEventHandler;
                        Client.Groups.RequestGroupRoles(corradeCommandParameters.Group.UUID);
                        if (!GroupRoleDataReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Groups.GroupRoleDataReply -= GroupRoleDataEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_ROLE_POWERS);
                        }
                        Client.Groups.GroupRoleDataReply -= GroupRoleDataEventHandler;
                    }
                    if (data.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                    }
                };
        }
    }
}