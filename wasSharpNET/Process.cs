///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System.Diagnostics;

namespace wasSharpNET
{
    public static class Process
    {
        /// <summary>
        ///     Retrieves the instance name from a processr.
        /// </summary>
        /// <param name="proc">the process</param>
        /// <returns>the instance name of a process or null if the instance name was not found</returns>
        /// <remarks>(C) Ingo Rammer</remarks>
        public static string GetProcessInstanceName(this System.Diagnostics.Process proc)
        {
            var cat = new PerformanceCounterCategory("Process");
            
            foreach (var instance in cat.GetInstanceNames())
            {
                using (var cnt = new PerformanceCounter("Process", "ID Process", instance, true))
                {
                    var val = (int) cnt.RawValue;
                    if (val == proc.Id)
                    {
                        return instance;
                    }
                }
            }
            return null;
        }
    }
}