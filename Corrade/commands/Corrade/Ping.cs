///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> ping = (commandGroup, message, result) =>
            {
                result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                    wasGetDescriptionFromEnumValue(ScriptKeys.PONG));
            };
        }
    }
}