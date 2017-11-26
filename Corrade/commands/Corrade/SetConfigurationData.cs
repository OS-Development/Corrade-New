///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using Corrade.Constants;
using CorradeConfigurationSharp;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                setconfigurationdata =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.System))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        Locks.ClientInstanceConfigurationLock.EnterReadLock();
                        corradeConfiguration = corradeConfiguration.wasCSVToStructure(wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                corradeCommandParameters.Message)), wasInput);
                        Locks.ClientInstanceConfigurationLock.ExitReadLock();
                        lock (ConfigurationFileLock)
                        {
                            try
                            {
                                using (var fileStream = new FileStream(CORRADE_CONSTANTS.CONFIGURATION_FILE,
                                    FileMode.Create, FileAccess.Write, FileShare.None, 16384, true))
                                {
                                    corradeConfiguration.Save(fileStream, ref corradeConfiguration);
                                }
                            }
                            catch (Exception ex)
                            {
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), ex.Message);
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.UNABLE_TO_SAVE_CORRADE_CONFIGURATION);
                            }
                        }
                    };
        }
    }
}