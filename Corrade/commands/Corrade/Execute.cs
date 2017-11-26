///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;
using System.Threading;
using CorradeConfigurationSharp;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> execute =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Execute))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var file =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FILE)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(file))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_EXECUTABLE_FILE_PROVIDED);
                    bool shellExecute;
                    if (!bool.TryParse(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SHELL)),
                            corradeCommandParameters.Message)), out shellExecute))
                        shellExecute = false;
                    bool createWindow;
                    if (!bool.TryParse(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.WINDOW)),
                            corradeCommandParameters.Message)), out createWindow))
                        createWindow = false;
                    var p = new ProcessStartInfo(file,
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PARAMETER)),
                            corradeCommandParameters.Message)))
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        UseShellExecute = shellExecute,
                        CreateNoWindow = !createWindow
                    };
                    var stdout = new StringBuilder();
                    var stderr = new StringBuilder();
                    ManualResetEventSlim[] StdEvent =
                    {
                        new ManualResetEventSlim(false),
                        new ManualResetEventSlim(false)
                    };
                    Process q;
                    try
                    {
                        q = Process.Start(p);
                    }
                    catch (Exception)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_START_PROCESS);
                    }
                    q.OutputDataReceived += (sender, output) =>
                    {
                        if (output.Data == null)
                        {
                            StdEvent[0].Set();
                            return;
                        }
                        stdout.AppendLine(output.Data);
                    };
                    q.ErrorDataReceived += (sender, output) =>
                    {
                        if (output.Data == null)
                        {
                            StdEvent[1].Set();
                            return;
                        }
                        stderr.AppendLine(output.Data);
                    };
                    q.BeginErrorReadLine();
                    q.BeginOutputReadLine();
                    if (!q.WaitForExit((int) corradeConfiguration.ServicesTimeout))
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_WAITING_FOR_EXECUTION);
                    if (StdEvent[0].Wait((int) corradeConfiguration.ServicesTimeout) && !stdout.Length.Equals(0))
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), stdout.ToString());
                    if (StdEvent[1].Wait((int) corradeConfiguration.ServicesTimeout) && !stderr.Length.Equals(0))
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), stderr.ToString());
                };
        }
    }
}