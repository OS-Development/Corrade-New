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
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getmembers =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    UUID groupUUID;
                    string target = wasInput(
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
                    IEnumerable<UUID> currentGroups = Enumerable.Empty<UUID>();
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
                    ManualResetEvent agentInGroupEvent = new ManualResetEvent(false);
                    List<string> csv = new List<string>();
                    Dictionary<UUID, GroupMember> groupMembers = new Dictionary<UUID, GroupMember>();
                    EventHandler<GroupMembersReplyEventArgs> HandleGroupMembersReplyDelegate = (sender, args) =>
                    {
                        groupMembers = args.Members;
                        agentInGroupEvent.Set();
                    };
                    lock (Locks.ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupMembersReply += HandleGroupMembersReplyDelegate;
                        Client.Groups.RequestGroupMembers(groupUUID);
                        if (!agentInGroupEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_GROUP_MEMBERS);
                        }
                        Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                    }
                    object LockObject = new object();
                    Parallel.ForEach(groupMembers, o =>
                    {
                        string agentName = string.Empty;
                        if (Resolvers.AgentUUIDToName(Client, o.Value.ID, corradeConfiguration.ServicesTimeout,
                            ref agentName))
                        {
                            lock (LockObject)
                            {
                                csv.Add(agentName);
                                csv.Add(o.Key.ToString());
                            }
                        }
                    });
                    if (csv.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                    }
                };
        }
    }
}