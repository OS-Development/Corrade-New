///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using CorradeConfiguration;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> writefile =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.System))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var path =
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.PATH)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(path))
                    {
                        throw new ScriptException(ScriptError.NO_PATH_PROVIDED);
                    }
                    var data = wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(data))
                    {
                        throw new ScriptException(ScriptError.NO_DATA_PROVIDED);
                    }
                    FileMode fileMode;
                    switch (Reflection.GetEnumValueFromName<Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Action.APPEND:
                            fileMode = FileMode.Append;
                            break;
                        case Action.CREATE:
                            fileMode = FileMode.Create;
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                    // Write to the file.
                    try
                    {
                        using (var fileStream = File.Open(path, fileMode, FileAccess.Write, FileShare.None))
                        {
                            using (var streamWriter = new StreamWriter(fileStream, Encoding.UTF8))
                            {
                                streamWriter.Write(data);
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA), ex.Message);
                        throw new ScriptException(ScriptError.UNABLE_TO_WRITE_FILE);
                    }
                };
        }
    }
}