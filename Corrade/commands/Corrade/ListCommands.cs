using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> listcommands =
                (commandGroup, message, result) =>
                {
                    HashSet<string> data = new HashSet<string>();
                    object LockObject = new object();
                    Parallel.ForEach(wasGetEnumDescriptions<ScriptKeys>(), o =>
                    {
                        ScriptKeys scriptKey = wasGetEnumValueFromDescription<ScriptKeys>(o);
                        IsCorradeCommandAttribute isCommandAttribute =
                            wasGetAttributeFromEnumValue<IsCorradeCommandAttribute>(scriptKey);
                        if (isCommandAttribute == null || !isCommandAttribute.IsCorradeCorradeCommand)
                            return;
                        CommandPermissionMaskAttribute commandPermissionMaskAttribute =
                            wasGetAttributeFromEnumValue<CommandPermissionMaskAttribute>(scriptKey);
                        if (commandPermissionMaskAttribute == null) return;
                        if (!commandGroup.Equals(default(Group)) &&
                            !(commandGroup.PermissionMask & commandPermissionMaskAttribute.PermissionMask).Equals(0))
                        {
                            lock (LockObject)
                            {
                                data.Add(o);
                            }
                        }
                    });
                    if (data.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA), wasEnumerableToCSV(data));
                    }
                };
        }
    }
}