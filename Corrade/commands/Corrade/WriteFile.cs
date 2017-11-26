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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> writefile =
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
                    var data =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(data))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_DATA_PROVIDED);
                    FileMode fileMode;
                    switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                    ))
                    {
                        case Enumerations.Action.APPEND:
                            fileMode = FileMode.Append;
                            break;

                        case Enumerations.Action.CREATE:
                            fileMode = FileMode.Create;
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                    // Write to the file.
                    try
                    {
                        using (
                            var fileStream = new FileStream(path, fileMode, FileAccess.Write, FileShare.None, 16384,
                                true))
                        {
                            using (var streamWriter = new StreamWriter(fileStream, Encoding.UTF8))
                            {
                                streamWriter.Write(data);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), ex.Message);
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_WRITE_FILE);
                    }
                };
        }
    }
}