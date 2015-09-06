using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> tag = (commandGroup, message, result) =>
            {
                if (
                    !HasCorradePermission(commandGroup.Name,
                        (int) Permissions.Grooming))
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
                if (!currentGroups.ToList().Any(o => o.Equals(commandGroup.UUID)))
                {
                    throw new ScriptException(ScriptError.NOT_IN_GROUP);
                }
                switch (wasGetEnumValueFromDescription<Action>(
                    wasInput(
                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)), message))
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
                        lock (ClientInstanceGroupsLock)
                        {
                            Client.Groups.GroupRoleDataReply += Groups_GroupRoleDataReply;
                            Client.Groups.RequestGroupRoles(commandGroup.UUID);
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
                                        wasKeyValueGet(
                                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TITLE)),
                                            message)),
                                    StringComparison.Ordinal));
                        switch (!role.Equals(default(KeyValuePair<string, UUID>)))
                        {
                            case false:
                                throw new ScriptException(ScriptError.COULD_NOT_FIND_TITLE);
                        }
                        Client.Groups.ActivateTitle(commandGroup.UUID, role.Value);
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
                        lock (ClientInstanceGroupsLock)
                        {
                            Client.Groups.GroupTitlesReply += GroupTitlesReplyEventHandler;
                            Client.Groups.RequestGroupTitles(commandGroup.UUID);
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
                            result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA), title);
                        }
                        break;
                    default:
                        throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                }
            };
        }
    }
}