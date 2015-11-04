///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using CorradeConfiguration;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> setconfigurationdata =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.System))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    lock (ConfigurationFileLock)
                    {
                        wasCSVToStructure(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                    corradeCommandParameters.Message)), ref corradeConfiguration);
                        UpdateDynamicConfiguration(corradeConfiguration);
                        ConfigurationWatcher.EnableRaisingEvents = false;
                        try
                        {
                            corradeConfiguration.Save(CORRADE_CONSTANTS.CONFIGURATION_FILE, ref corradeConfiguration);
                        }
                        catch (Exception)
                        {
                            throw new ScriptException(ScriptError.UNABLE_TO_SAVE_CORRADE_CONFIGURATION);
                        }
                        ConfigurationWatcher.EnableRaisingEvents = true;
                    }
                };
        }
    }
}