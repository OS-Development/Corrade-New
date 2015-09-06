using System;
using System.Collections.Generic;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> setconfigurationdata =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.System))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    lock (ConfigurationFileLock)
                    {
                        wasCSVToStructure(
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DATA)),
                                message)), ref corradeConfiguration);
                        corradeConfiguration.UpdateDynamicConfiguration(corradeConfiguration);
                        ConfigurationWatcher.EnableRaisingEvents = false;
                        corradeConfiguration.Save(CORRADE_CONSTANTS.CONFIGURATION_FILE, ref corradeConfiguration);
                        ConfigurationWatcher.EnableRaisingEvents = true;
                    }
                };
        }
    }
}