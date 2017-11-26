///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CorradeConfigurationSharp;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> readfile =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.System))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var path =
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PATH)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(path))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_PATH_PROVIDED);
                    string data;
                    // Read from file.
                    try
                    {
                        using (
                            var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 16384,
                                true))
                        {
                            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                            {
                                data = streamReader.ReadToEnd();
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), ex.Message);
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_READ_FILE);
                    }
                    if (!string.IsNullOrEmpty(data))
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), data);
                };
        }
    }
}