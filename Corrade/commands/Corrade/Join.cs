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
            public static Action<Group, string, Dictionary<string, string>> join = (commandGroup, message, result) =>
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
                if (currentGroups.ToList().Any(o => o.Equals(commandGroup.UUID)))
                {
                    throw new ScriptException(ScriptError.ALREADY_IN_GROUP);
                }
                OpenMetaverse.Group targetGroup = new OpenMetaverse.Group();
                if (!RequestGroup(commandGroup.UUID, corradeConfiguration.ServicesTimeout, ref targetGroup))
                {
                    throw new ScriptException(ScriptError.GROUP_NOT_FOUND);
                }
                if (!targetGroup.OpenEnrollment)
                {
                    throw new ScriptException(ScriptError.GROUP_NOT_OPEN);
                }
                if (!Client.Network.MaxAgentGroups.Equals(-1))
                {
                    if (currentGroups.ToList().Count >= Client.Network.MaxAgentGroups)
                    {
                        throw new ScriptException(ScriptError.MAXIMUM_NUMBER_OF_GROUPS_REACHED);
                    }
                }
                ManualResetEvent GroupJoinedReplyEvent = new ManualResetEvent(false);
                EventHandler<GroupOperationEventArgs> GroupOperationEventHandler =
                    (sender, args) => GroupJoinedReplyEvent.Set();
                lock (ClientInstanceGroupsLock)
                {
                    Client.Groups.GroupJoinedReply += GroupOperationEventHandler;
                    Client.Groups.RequestJoinGroup(commandGroup.UUID);
                    if (!GroupJoinedReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                    {
                        Client.Groups.GroupJoinedReply -= GroupOperationEventHandler;
                        throw new ScriptException(ScriptError.TIMEOUT_JOINING_GROUP);
                    }
                    Client.Groups.GroupJoinedReply -= GroupOperationEventHandler;
                }
                if (
                    !GetCurrentGroups(corradeConfiguration.ServicesTimeout,
                        ref currentGroups))
                {
                    throw new ScriptException(ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                }
                if (!currentGroups.Any(o => o.Equals(commandGroup.UUID)))
                {
                    throw new ScriptException(ScriptError.COULD_NOT_JOIN_GROUP);
                }
            };
        }
    }
}