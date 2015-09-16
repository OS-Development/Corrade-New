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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> gettitles =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Group))
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
                    lock (ClientInstanceGroupsLock)
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
                    foreach (KeyValuePair<UUID, GroupTitle> title in groupTitles)
                    {
                        string roleName = string.Empty;
                        if (
                            !RoleUUIDToName(title.Value.RoleID, corradeCommandParameters.Group.UUID,
                                corradeConfiguration.ServicesTimeout,
                                corradeConfiguration.DataTimeout,
                                ref roleName))
                            continue;
                        csv.Add(title.Value.Title);
                        csv.Add(title.Key.ToString());
                        csv.Add(roleName);
                        csv.Add(title.Value.RoleID.ToString());
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