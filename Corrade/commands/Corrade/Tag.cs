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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> tag =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
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
                    switch (Reflection.GetEnumValueFromName<Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Action.SET:
                            ManualResetEvent GroupRoleDataReplyEvent = new ManualResetEvent(false);
                            Dictionary<string, UUID> roleData = new Dictionary<string, UUID>();
                            EventHandler<GroupRolesDataReplyEventArgs> Groups_GroupRoleDataReply = (sender, args) =>
                            {
                                roleData = args.Roles.ToDictionary(o => o.Value.Title, o => o.Value.ID);
                                GroupRoleDataReplyEvent.Set();
                            };
                            lock (Locks.ClientInstanceGroupsLock)
                            {
                                Client.Groups.GroupRoleDataReply += Groups_GroupRoleDataReply;
                                Client.Groups.RequestGroupRoles(corradeCommandParameters.Group.UUID);
                                if (
                                    !GroupRoleDataReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout,
                                        false))
                                {
                                    Client.Groups.GroupRoleDataReply -= Groups_GroupRoleDataReply;
                                    throw new ScriptException(ScriptError.TIMEOUT_GETTING_GROUP_ROLES);
                                }
                                Client.Groups.GroupRoleDataReply -= Groups_GroupRoleDataReply;
                            }
                            KeyValuePair<string, UUID> role = roleData.AsParallel().FirstOrDefault(
                                o =>
                                    o.Key.Equals(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TITLE)),
                                                corradeCommandParameters.Message)),
                                        StringComparison.Ordinal));
                            switch (!role.Equals(default(KeyValuePair<string, UUID>)))
                            {
                                case false:
                                    throw new ScriptException(ScriptError.COULD_NOT_FIND_TITLE);
                            }
                            Client.Groups.ActivateTitle(corradeCommandParameters.Group.UUID, role.Value);
                            break;
                        case Action.GET:
                            string title = string.Empty;
                            ManualResetEvent GroupTitlesReplyEvent = new ManualResetEvent(false);
                            EventHandler<GroupTitlesReplyEventArgs> GroupTitlesReplyEventHandler = (sender, args) =>
                            {
                                KeyValuePair<UUID, GroupTitle> pair =
                                    args.Titles.AsParallel().FirstOrDefault(o => o.Value.Selected);
                                if (!pair.Equals(default(KeyValuePair<UUID, GroupTitle>)))
                                {
                                    title = pair.Value.Title;
                                }
                                GroupTitlesReplyEvent.Set();
                            };
                            lock (Locks.ClientInstanceGroupsLock)
                            {
                                Client.Groups.GroupTitlesReply += GroupTitlesReplyEventHandler;
                                Client.Groups.RequestGroupTitles(corradeCommandParameters.Group.UUID);
                                if (
                                    !GroupTitlesReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    Client.Groups.GroupTitlesReply -= GroupTitlesReplyEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_GETTING_GROUP_TITLES);
                                }
                                Client.Groups.GroupTitlesReply -= GroupTitlesReplyEventHandler;
                            }
                            if (!title.Equals(string.Empty))
                            {
                                result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA), title);
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}