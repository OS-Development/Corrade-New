///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using Corrade.Constants;
using CorradeConfiguration;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Command.CorradeCommandParameters, Dictionary<string, string>> setconfigurationdata =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.System))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    lock (ConfigurationFileLock)
                    {
                        /*Reflection.wasCSVToStructure(Client, corradeConfiguration.ServicesTimeout,
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(wasSharp.Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                    corradeCommandParameters.Message)), ref corradeConfiguration);*/
                        corradeConfiguration.wasCSVToStructure(Client, corradeConfiguration.ServicesTimeout,
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                    corradeCommandParameters.Message)));
                        UpdateDynamicConfiguration(corradeConfiguration);
                        ConfigurationWatcher.EnableRaisingEvents = false;
                        try
                        {
                            corradeConfiguration.Save(CORRADE_CONSTANTS.CONFIGURATION_FILE, ref corradeConfiguration);
                        }
                        catch (Exception)
                        {
                            throw new Command.ScriptException(
                                Enumerations.ScriptError.UNABLE_TO_SAVE_CORRADE_CONFIGURATION);
                        }
                        ConfigurationWatcher.EnableRaisingEvents = true;
                    }
                };
        }
    }
}