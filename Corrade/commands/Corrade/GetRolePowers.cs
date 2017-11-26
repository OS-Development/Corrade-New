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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getrolepowers =
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
                    var role =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ROLE)),
                            corradeCommandParameters.Message));
                    UUID roleUUID;
                    if (!UUID.TryParse(role, out roleUUID) &&
                        !Resolvers.RoleNameToUUID(Client, role, groupUUID,
                            corradeConfiguration.ServicesTimeout,
                            ref roleUUID))
                        throw new Command.ScriptException(Enumerations.ScriptError.ROLE_NOT_FOUND);
                    var data = new HashSet<string>();
                    var GroupRoleDataReplyEvent = new ManualResetEventSlim(false);
                    var requestUUID = UUID.Zero;
                    EventHandler<GroupRolesDataReplyEventArgs> GroupRoleDataEventHandler = (sender, args) =>
                    {
                        if (!requestUUID.Equals(args.RequestID) || !args.GroupID.Equals(groupUUID))
                            return;

                        var queryRole =
                            args.Roles.Values.AsParallel().FirstOrDefault(o => o.ID.Equals(roleUUID));
                        data.UnionWith(typeof(GroupPowers).GetFields(BindingFlags.Public | BindingFlags.Static)
                            .AsParallel().Where(
                                o => queryRole.Powers.IsMaskFlagSet((GroupPowers) o.GetValue(null)))
                            .Select(o => o.Name));
                        GroupRoleDataReplyEvent.Set();
                    };
                    Client.Groups.GroupRoleDataReply += GroupRoleDataEventHandler;
                    requestUUID = Client.Groups.RequestGroupRoles(groupUUID);
                    if (!GroupRoleDataReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Groups.GroupRoleDataReply -= GroupRoleDataEventHandler;
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_ROLE_POWERS);
                    }
                    Client.Groups.GroupRoleDataReply -= GroupRoleDataEventHandler;
                    if (data.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                };
        }
    }
}