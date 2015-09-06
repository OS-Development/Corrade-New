using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> getanimations =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Grooming))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    List<string> csv = new List<string>();
                    Client.Self.SignaledAnimations.ForEach(
                        o =>
                            csv.AddRange(new List<string>
                            {
                                o.Key.ToString(),
                                o.Value.ToString(CultureInfo.DefaultThreadCurrentCulture)
                            }));
                    if (csv.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(csv));
                    }
                };
        }
    }
}