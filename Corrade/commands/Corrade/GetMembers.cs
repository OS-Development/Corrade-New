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

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> getmembers =
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
                    if (!currentGroups.ToList().Any(o => o.Equals(commandGroup.UUID)))
                    {
                        throw new ScriptException(ScriptError.NOT_IN_GROUP);
                    }
                    ManualResetEvent agentInGroupEvent = new ManualResetEvent(false);
                    List<string> csv = new List<string>();
                    Dictionary<UUID, GroupMember> groupMembers = new Dictionary<UUID, GroupMember>();
                    EventHandler<GroupMembersReplyEventArgs> HandleGroupMembersReplyDelegate = (sender, args) =>
                    {
                        groupMembers = args.Members;
                        agentInGroupEvent.Set();
                    };
                    lock (ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupMembersReply += HandleGroupMembersReplyDelegate;
                        Client.Groups.RequestGroupMembers(commandGroup.UUID);
                        if (!agentInGroupEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_GROUP_MEMBERS);
                        }
                        Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                    }
                    foreach (KeyValuePair<UUID, GroupMember> pair in groupMembers)
                    {
                        string agentName = string.Empty;
                        switch (
                            !AgentUUIDToName(pair.Value.ID, corradeConfiguration.ServicesTimeout, ref agentName))
                        {
                            case true:
                                continue;
                            default:
                                csv.Add(agentName);
                                csv.Add(pair.Key.ToString());
                                break;
                        }
                    }
                    if (csv.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(csv));
                    }
                };
        }
    }
}