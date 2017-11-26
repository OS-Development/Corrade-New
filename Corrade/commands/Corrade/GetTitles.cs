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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> gettitles =
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
                    var csv = new List<string>();
                    var groupTitles = new Dictionary<UUID, GroupTitle>();
                    var GroupTitlesReplyEvent = new ManualResetEventSlim(false);
                    var requestUUID = UUID.Zero;
                    EventHandler<GroupTitlesReplyEventArgs> GroupTitlesReplyEventHandler = (sender, args) =>
                    {
                        if (!requestUUID.Equals(args.RequestID) || !args.GroupID.Equals(groupUUID))
                            return;
                        groupTitles = args.Titles;
                        GroupTitlesReplyEvent.Set();
                    };
                    Client.Groups.GroupTitlesReply += GroupTitlesReplyEventHandler;
                    requestUUID = Client.Groups.RequestGroupTitles(groupUUID);
                    if (!GroupTitlesReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Groups.GroupTitlesReply -= GroupTitlesReplyEventHandler;
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_GROUP_TITLES);
                    }
                    Client.Groups.GroupTitlesReply -= GroupTitlesReplyEventHandler;
                    var LockObject = new object();
                    groupTitles.AsParallel().ForAll(o =>
                    {
                        var roleName = string.Empty;
                        if (Resolvers.RoleUUIDToName(Client, o.Value.RoleID, groupUUID,
                            corradeConfiguration.ServicesTimeout,
                            ref roleName))
                            lock (LockObject)
                            {
                                csv.AddRange(new[]
                                {
                                    o.Value.Title,
                                    o.Key.ToString(),
                                    roleName,
                                    o.Value.RoleID.ToString()
                                });
                            }
                    });
                    if (csv.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                };
        }
    }
}