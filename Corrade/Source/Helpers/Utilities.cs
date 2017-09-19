///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using wasSharp;

namespace Corrade.Helpers
{
    public class Utilities
    {
        /// <summary>
        ///     Determines whether a string is a Corrade command.
        /// </summary>
        /// <returns>true if the string is a Corrade command</returns>
        public static readonly Func<string, bool> IsCorradeCommand = o =>
        {
            var data = KeyValue.Decode(o);
            return data.Any() && data.ContainsKey(Reflection.GetNameFromEnumValue(Command.ScriptKeys.COMMAND)) &&
                   data.ContainsKey(Reflection.GetNameFromEnumValue(Command.ScriptKeys.GROUP)) &&
                   data.ContainsKey(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PASSWORD));
        };
    }
}
