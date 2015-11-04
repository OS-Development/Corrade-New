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
using CorradeConfiguration;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> execute =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Execute))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string file =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FILE)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(file))
                    {
                        throw new ScriptException(ScriptError.NO_EXECUTABLE_FILE_PROVIDED);
                    }
                    ProcessStartInfo p = new ProcessStartInfo(file,
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.PARAMETER)),
                            corradeCommandParameters.Message)))
                    {
                        RedirectStandardOutput = true,
                        RedirectStandardError = true,
                        WindowStyle = ProcessWindowStyle.Normal,
                        UseShellExecute = false
                    };
                    StringBuilder stdout = new StringBuilder();
                    StringBuilder stderr = new StringBuilder();
                    ManualResetEvent[] StdEvent =
                    {
                        new ManualResetEvent(false),
                        new ManualResetEvent(false)
                    };
                    Process q;
                    try
                    {
                        q = Process.Start(p);
                    }
                    catch (Exception)
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_START_PROCESS);
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
                    {
                        throw new ScriptException(ScriptError.TIMEOUT_WAITING_FOR_EXECUTION);
                    }
                    if (StdEvent[0].WaitOne((int) corradeConfiguration.ServicesTimeout) && !stdout.Length.Equals(0))
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA), stdout.ToString());
                    }
                    if (StdEvent[1].WaitOne((int) corradeConfiguration.ServicesTimeout) && !stderr.Length.Equals(0))
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA), stderr.ToString());
                    }
                };
        }
    }
}