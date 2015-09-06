using System;
using System.Collections.Generic;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> version = (commandGroup, message, result) =>
            {
                result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                    CORRADE_CONSTANTS.CORRADE_VERSION);
            };
        }
    }
}