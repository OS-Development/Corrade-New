///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> getbalance =
                (commandGroup, message, result) =>
                {
                    if (
                        !HasCorradePermission(commandGroup.Name, (int) Permissions.Economy))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    if (!UpdateBalance(corradeConfiguration.ServicesTimeout))
                    {
                        throw new ScriptException(ScriptError.UNABLE_TO_OBTAIN_MONEY_BALANCE);
                    }
                    result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                        Client.Self.Balance.ToString(CultureInfo.DefaultThreadCurrentCulture));
                };
        }
    }
}