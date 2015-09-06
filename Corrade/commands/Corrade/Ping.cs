using System;
using System.Collections.Generic;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> ping = (commandGroup, message, result) =>
            {
                result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                    wasGetDescriptionFromEnumValue(ScriptKeys.PONG));
            };
        }
    }
}