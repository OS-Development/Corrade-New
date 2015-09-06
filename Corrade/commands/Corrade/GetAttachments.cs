using System;
using System.Collections.Generic;
using System.Linq;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> getattachments =
                (commandGroup, message, result) =>
                {
                    if (
                        !HasCorradePermission(commandGroup.Name,
                            (int) Permissions.Grooming))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    List<string> attachments = GetAttachments(
                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout)
                        .AsParallel()
                        .Select(o => new[]
                        {
                            o.Value.ToString(),
                            o.Key.Properties.Name
                        }).SelectMany(o => o).ToList();
                    if (attachments.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(attachments));
                    }
                };
        }
    }
}