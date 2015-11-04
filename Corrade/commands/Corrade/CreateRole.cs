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
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> createrole =
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
                        !HasGroupPowers(Client.Self.AgentID, corradeCommandParameters.Group.UUID, GroupPowers.CreateRole,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    ManualResetEvent GroupRoleDataReplyEvent = new ManualResetEvent(false);
                    int roleCount = 0;
                    EventHandler<GroupRolesDataReplyEventArgs> GroupRolesDataEventHandler = (sender, args) =>
                    {
                        roleCount = args.Roles.Count;
                        GroupRoleDataReplyEvent.Set();
                    };
                    lock (ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupRoleDataReply += GroupRolesDataEventHandler;
                        Client.Groups.RequestGroupRoles(corradeCommandParameters.Group.UUID);
                        if (!GroupRoleDataReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Groups.GroupRoleDataReply -= GroupRolesDataEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_GROUP_ROLES);
                        }
                        Client.Groups.GroupRoleDataReply -= GroupRolesDataEventHandler;
                    }
                    if (IsSecondLife() && roleCount >= LINDEN_CONSTANTS.GROUPS.MAXIMUM_NUMBER_OF_ROLES)
                    {
                        throw new ScriptException(ScriptError.MAXIMUM_NUMBER_OF_ROLES_EXCEEDED);
                    }
                    string role =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ROLE)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(role))
                    {
                        throw new ScriptException(ScriptError.NO_ROLE_NAME_SPECIFIED);
                    }
                    ulong powers = 0;
                    Parallel.ForEach(CSV.ToEnumerable(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.POWERS)),
                                corradeCommandParameters.Message))).AsParallel().Where(o => !string.IsNullOrEmpty(o)),
                        o =>
                            Parallel.ForEach(
                                typeof (GroupPowers).GetFields(BindingFlags.Public | BindingFlags.Static)
                                    .AsParallel().Where(p => p.Name.Equals(o, StringComparison.Ordinal)),
                                q => { powers |= ((ulong) q.GetValue(null)); }));
                    if (
                        !HasGroupPowers(Client.Self.AgentID, corradeCommandParameters.Group.UUID,
                            GroupPowers.ChangeActions,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    string title = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TITLE)),
                        corradeCommandParameters.Message));
                    if (IsSecondLife() && title.Length > LINDEN_CONSTANTS.GROUPS.MAXIMUM_GROUP_TITLE_LENGTH)
                    {
                        throw new ScriptException(ScriptError.TOO_MANY_CHARACTERS_FOR_GROUP_TITLE);
                    }
                    Client.Groups.CreateRole(corradeCommandParameters.Group.UUID, new GroupRole
                    {
                        Name = role,
                        Description =
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DESCRIPTION)),
                                    corradeCommandParameters.Message)),
                        GroupID = corradeCommandParameters.Group.UUID,
                        ID = UUID.Random(),
                        Powers = (GroupPowers) powers,
                        Title = title
                    });
                    UUID roleUUID = UUID.Zero;
                    if (
                        !RoleNameToUUID(role, corradeCommandParameters.Group.UUID,
                            corradeConfiguration.ServicesTimeout, ref roleUUID))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_CREATE_ROLE);
                    }
                };
        }
    }
}