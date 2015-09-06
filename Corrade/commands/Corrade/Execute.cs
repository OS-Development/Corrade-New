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

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> execute = (commandGroup, message, result) =>
            {
                if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Execute))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                string file =
                    wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.FILE)),
                        message));
                if (string.IsNullOrEmpty(file))
                {
                    throw new ScriptException(ScriptError.NO_EXECUTABLE_FILE_PROVIDED);
                }
                ProcessStartInfo p = new ProcessStartInfo(file,
                    wasInput(wasKeyValueGet(
                        wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.PARAMETER)), message)))
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
                    result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA), stdout.ToString());
                }
                if (StdEvent[1].WaitOne((int) corradeConfiguration.ServicesTimeout) && !stderr.Length.Equals(0))
                {
                    result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA), stderr.ToString());
                }
            };
        }
    }
}