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
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Timers;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> createrole =
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
                            GroupPowers.CreateRole,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    var GroupRoleDataReplyEvent = new ManualResetEventSlim(false);
                    var roleCount = 0;
                    var requestUUID = UUID.Zero;
                    EventHandler<GroupRolesDataReplyEventArgs> GroupRolesDataEventHandler = (sender, args) =>
                    {
                        if (!requestUUID.Equals(args.GroupID) || !args.GroupID.Equals(groupUUID))
                            return;
                        roleCount = args.Roles.Count;
                        GroupRoleDataReplyEvent.Set();
                    };
                    Client.Groups.GroupRoleDataReply += GroupRolesDataEventHandler;
                    requestUUID = Client.Groups.RequestGroupRoles(groupUUID);
                    if (!GroupRoleDataReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Groups.GroupRoleDataReply -= GroupRolesDataEventHandler;
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_GROUP_ROLES);
                    }
                    Client.Groups.GroupRoleDataReply -= GroupRolesDataEventHandler;
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                        roleCount >= wasOpenMetaverse.Constants.GROUPS.MAXIMUM_NUMBER_OF_ROLES)
                        throw new Command.ScriptException(Enumerations.ScriptError.MAXIMUM_NUMBER_OF_ROLES_EXCEEDED);
                    var role =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ROLE)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(role))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ROLE_NAME_SPECIFIED);
                    var powers = GroupPowers.None;
                    CSV.ToEnumerable(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POWERS)),
                                    corradeCommandParameters.Message)))
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o))
                        .ForAll(
                            o =>
                                typeof(GroupPowers).GetFields(BindingFlags.Public | BindingFlags.Static)
                                    .AsParallel()
                                    .Where(p => string.Equals(o, p.Name, StringComparison.Ordinal))
                                    .ForAll(
                                        q =>
                                        {
                                            BitTwiddling.SetMaskFlag(ref powers, (GroupPowers) q.GetValue(null));
                                        }));
                    if (
                        !Services.HasGroupPowers(Client, Client.Self.AgentID, groupUUID,
                            GroupPowers.ChangeActions,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    var title = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TITLE)),
                        corradeCommandParameters.Message));
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                        title.Length > wasOpenMetaverse.Constants.GROUPS.MAXIMUM_GROUP_TITLE_LENGTH)
                        throw new Command.ScriptException(Enumerations.ScriptError.TOO_MANY_CHARACTERS_FOR_GROUP_TITLE);
                    Client.Groups.CreateRole(groupUUID, new GroupRole
                    {
                        Name = role,
                        Description =
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                    corradeCommandParameters.Message)),
                        GroupID = groupUUID,
                        ID = UUID.Random(),
                        Powers = powers,
                        Title = title
                    });
                    var roleUUID = UUID.Zero;
                    if (
                        !Resolvers.RoleNameToUUID(Client, role, groupUUID,
                            corradeConfiguration.ServicesTimeout, ref roleUUID))
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_CREATE_ROLE);
                };
        }
    }
}