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
            public static Action<Group, string, Dictionary<string, string>> run = (commandGroup, message, result) =>
            {
                if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Movement))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                Action action = wasGetEnumValueFromDescription<Action>(
                    wasInput(
                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)), message))
                        .ToLowerInvariant());
                switch (action)
                {
                    case Action.ENABLE:
                    case Action.DISABLE:
                        Client.Self.Movement.AlwaysRun = !action.Equals(Action.DISABLE);
                        Client.Self.Movement.SendUpdate(true);
                        break;
                    case Action.GET:
                        result.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                            Client.Self.Movement.AlwaysRun.ToString());
                        break;
                    default:
                        throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                }
            };
        }
    }
}