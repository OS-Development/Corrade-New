using System;
using System.Collections.Generic;
using System.Linq;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> getconnectedregions =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                        wasEnumerableToCSV(Client.Network.Simulators.Select(o => o.Name)));
                };
        }
    }
}