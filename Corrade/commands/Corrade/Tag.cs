///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> tag =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
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
                    var requestUUID = UUID.Zero;
                    switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                    ))
                    {
                        case Enumerations.Action.SET:
                            var GroupRoleDataReplyEvent = new ManualResetEventSlim(false);
                            var roleData = new Dictionary<string, UUID>();
                            EventHandler<GroupRolesDataReplyEventArgs> Groups_GroupRoleDataReply = (sender, args) =>
                            {
                                if (!requestUUID.Equals(args.RequestID) || !args.GroupID.Equals(groupUUID))
                                    return;

                                roleData = args
                                    .Roles
                                    .GroupBy(o => o.Value.Title)
                                    .ToDictionary(o => o.Key, o => o.FirstOrDefault().Value.ID);
                                GroupRoleDataReplyEvent.Set();
                            };
                            Client.Groups.GroupRoleDataReply += Groups_GroupRoleDataReply;
                            requestUUID = Client.Groups.RequestGroupRoles(groupUUID);
                            if (
                                !GroupRoleDataReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                            {
                                Client.Groups.GroupRoleDataReply -= Groups_GroupRoleDataReply;
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_GETTING_GROUP_ROLES);
                            }
                            Client.Groups.GroupRoleDataReply -= Groups_GroupRoleDataReply;
                            var role = roleData.AsParallel().FirstOrDefault(
                                o =>
                                    o.Key.Equals(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TITLE)),
                                                corradeCommandParameters.Message)),
                                        StringComparison.Ordinal));
                            switch (!role.Equals(default(KeyValuePair<string, UUID>)))
                            {
                                case false:
                                    throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_TITLE);
                            }
                            Client.Groups.ActivateTitle(groupUUID, role.Value);
                            break;

                        case Enumerations.Action.GET:
                            var title = string.Empty;
                            var GroupTitlesReplyEvent = new ManualResetEventSlim(false);
                            EventHandler<GroupTitlesReplyEventArgs> GroupTitlesReplyEventHandler = (sender, args) =>
                            {
                                if (!args.RequestID.Equals(requestUUID) || !args.GroupID.Equals(groupUUID))
                                    return;

                                var pair =
                                    args.Titles.AsParallel().FirstOrDefault(o => o.Value.Selected);
                                if (!pair.Equals(default(KeyValuePair<UUID, GroupTitle>)))
                                    title = pair.Value.Title;
                                GroupTitlesReplyEvent.Set();
                            };
                            Client.Groups.GroupTitlesReply += GroupTitlesReplyEventHandler;
                            requestUUID = Client.Groups.RequestGroupTitles(groupUUID);
                            if (
                                !GroupTitlesReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                            {
                                Client.Groups.GroupTitlesReply -= GroupTitlesReplyEventHandler;
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_GETTING_GROUP_TITLES);
                            }
                            Client.Groups.GroupTitlesReply -= GroupTitlesReplyEventHandler;
                            if (!title.Equals(string.Empty))
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), title);
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}