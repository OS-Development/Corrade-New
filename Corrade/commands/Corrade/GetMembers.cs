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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getmembers =
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
                    var agentInGroupEvent = new ManualResetEvent(false);
                    var csv = new List<string>();
                    var groupMembers = new Dictionary<UUID, GroupMember>();
                    var groupMembersRequestUUID = UUID.Zero;
                    EventHandler<GroupMembersReplyEventArgs> HandleGroupMembersReplyDelegate = (sender, args) =>
                    {
                        if (!groupMembersRequestUUID.Equals(args.RequestID)) return;
                        groupMembers = args.Members;
                        agentInGroupEvent.Set();
                    };
                    Client.Groups.GroupMembersReply += HandleGroupMembersReplyDelegate;
                    groupMembersRequestUUID = Client.Groups.RequestGroupMembers(groupUUID);
                    if (!agentInGroupEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                    {
                        Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;
                        throw new ScriptException(ScriptError.TIMEOUT_GETTING_GROUP_MEMBERS);
                    }
                    Client.Groups.GroupMembersReply -= HandleGroupMembersReplyDelegate;

                    var LockObject = new object();
                    groupMembers.AsParallel().ForAll(o =>
                    {
                        var agentName = string.Empty;
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