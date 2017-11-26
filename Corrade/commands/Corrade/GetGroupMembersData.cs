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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getgroupmembersdata =
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
                        Dictionary<UUID, GroupMember> groupMembers = null;
                        var groupMembersReceivedEvent = new ManualResetEventSlim(false);
                        var groupMembersRequestUUID = UUID.Zero;
                        EventHandler<GroupMembersReplyEventArgs> GroupMembersReplyEventHandler = (sender, args) =>
                        {
                            if (!groupMembersRequestUUID.Equals(args.RequestID)) return;
                            groupMembers = args.Members;
                            groupMembersReceivedEvent.Set();
                        };
                        Client.Groups.GroupMembersReply += GroupMembersReplyEventHandler;
                        groupMembersRequestUUID = Client.Groups.RequestGroupMembers(groupUUID);
                        if (
                            !groupMembersReceivedEvent.Wait(
                                (int) corradeConfiguration.ServicesTimeout))
                        {
                            Client.Groups.GroupMembersReply -= GroupMembersReplyEventHandler;
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_GROUP_MEMBERS);
                        }
                        Client.Groups.GroupMembersReply -= GroupMembersReplyEventHandler;

                        if (groupMembers == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                        var data = new List<string>();
                        var LockObject = new object();
                        groupMembers.Values.AsParallel().ForAll(o =>
                        {
                            var groupMemberData =
                                o.GetStructuredData(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                            corradeCommandParameters.Message)));
                            lock (LockObject)
                            {
                                data.AddRange(groupMemberData);
                            }
                        });
                        if (data.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(data));
                    };
        }
    }
}