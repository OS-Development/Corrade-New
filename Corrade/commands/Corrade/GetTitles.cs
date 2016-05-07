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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> gettitles =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Group))
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
                    List<string> csv = new List<string>();
                    Dictionary<UUID, GroupTitle> groupTitles = new Dictionary<UUID, GroupTitle>();
                    ManualResetEvent GroupTitlesReplyEvent = new ManualResetEvent(false);
                    EventHandler<GroupTitlesReplyEventArgs> GroupTitlesReplyEventHandler = (sender, args) =>
                    {
                        groupTitles = args.Titles;
                        GroupTitlesReplyEvent.Set();
                    };
                    lock (Locks.ClientInstanceGroupsLock)
                    {
                        Client.Groups.GroupTitlesReply += GroupTitlesReplyEventHandler;
                        Client.Groups.RequestGroupTitles(corradeCommandParameters.Group.UUID);
                        if (!GroupTitlesReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Groups.GroupTitlesReply -= GroupTitlesReplyEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_GROUP_TITLES);
                        }
                        Client.Groups.GroupTitlesReply -= GroupTitlesReplyEventHandler;
                    }
                    object LockObject = new object();
                    Parallel.ForEach(groupTitles, o =>
                    {
                        string roleName = string.Empty;
                        if (Resolvers.RoleUUIDToName(Client, o.Value.RoleID, corradeCommandParameters.Group.UUID,
                            corradeConfiguration.ServicesTimeout,
                            ref roleName))
                        {
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