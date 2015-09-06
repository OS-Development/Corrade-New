using System;
using System.Collections.Generic;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> rlv = (commandGroup, message, result) =>
            {
                if (!HasCorradePermission(commandGroup.Name, (int) Permissions.System))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                switch (wasGetEnumValueFromDescription<Action>(
                    wasInput(
                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)),
                            message)).ToLowerInvariant()))
                {
                    case Action.ENABLE:
                        corradeConfiguration.EnableRLV = true;
                        break;
                    case Action.DISABLE:
                        corradeConfiguration.EnableRLV = false;
                        lock (RLVRulesLock)
                        {
                            RLVRules.Clear();
                        }
                        break;
                }
            };
        }
    }
}