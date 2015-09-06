using System;
using System.Collections.Generic;
using System.Linq;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> getfriendshiprequests =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Friendship))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    List<string> csv = new List<string>();
                    Client.Friends.FriendRequests.ForEach(o =>
                    {
                        string name = string.Empty;
                        if (
                            !AgentUUIDToName(o.Key, corradeConfiguration.ServicesTimeout,
                                ref name))
                        {
                            return;
                        }
                        csv.Add(name);
                        csv.Add(o.Key.ToString());
                    });
                    if (csv.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(csv));
                    }
                };
        }
    }
}