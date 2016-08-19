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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> createrole =
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
                            GroupPowers.CreateRole,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    var GroupRoleDataReplyEvent = new ManualResetEvent(false);
                    var roleCount = 0;
                    EventHandler<GroupRolesDataReplyEventArgs> GroupRolesDataEventHandler = (sender, args) =>
                    {
                        roleCount = args.Roles.Count;
                        GroupRoleDataReplyEvent.Set();
                    };
                    lock (Locks.ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupRoleDataReply += GroupRolesDataEventHandler;
                        Client.Groups.RequestGroupRoles(groupUUID);
                        if (!GroupRoleDataReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Groups.GroupRoleDataReply -= GroupRolesDataEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_GROUP_ROLES);
                        }
                        Client.Groups.GroupRoleDataReply -= GroupRolesDataEventHandler;
                    }
                    if (Helpers.IsSecondLife(Client) && roleCount >= Constants.GROUPS.MAXIMUM_NUMBER_OF_ROLES)
                    {
                        throw new ScriptException(ScriptError.MAXIMUM_NUMBER_OF_ROLES_EXCEEDED);
                    }
                    var role =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ROLE)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(role))
                    {
                        throw new ScriptException(ScriptError.NO_ROLE_NAME_SPECIFIED);
                    }
                    var powers = GroupPowers.None;
                    CSV.ToEnumerable(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.POWERS)),
                                corradeCommandParameters.Message)))
                        .ToArray()
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o))
                        .ForAll(
                            o =>
                                typeof (GroupPowers).GetFields(BindingFlags.Public | BindingFlags.Static)
                                    .AsParallel()
                                    .Where(p => Strings.Equals(o, p.Name, StringComparison.Ordinal))
                                    .ForAll(
                                        q => { BitTwiddling.SetMaskFlag(ref powers, (GroupPowers) q.GetValue(null)); }));
                    if (
                        !Services.HasGroupPowers(Client, Client.Self.AgentID, groupUUID,
                            GroupPowers.ChangeActions,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    var title = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TITLE)),
                        corradeCommandParameters.Message));
                    if (Helpers.IsSecondLife(Client) && title.Length > Constants.GROUPS.MAXIMUM_GROUP_TITLE_LENGTH)
                    {
                        throw new ScriptException(ScriptError.TOO_MANY_CHARACTERS_FOR_GROUP_TITLE);
                    }
                    lock (Locks.ClientInstanceGroupsLock)
                    {
                        Client.Groups.CreateRole(groupUUID, new GroupRole
                        {
                            Name = role,
                            Description =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DESCRIPTION)),
                                        corradeCommandParameters.Message)),
                            GroupID = groupUUID,
                            ID = UUID.Random(),
                            Powers = powers,
                            Title = title
                        });
                    }
                    var roleUUID = UUID.Zero;
                    if (
                        !Resolvers.RoleNameToUUID(Client, role, groupUUID,
                            corradeConfiguration.ServicesTimeout, ref roleUUID))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_CREATE_ROLE);
                    }
                };
        }
    }
}