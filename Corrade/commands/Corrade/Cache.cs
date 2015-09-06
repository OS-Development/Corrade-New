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
            public static Action<Group, string, Dictionary<string, string>> cache = (commandGroup, message, result) =>
            {
                if (!HasCorradePermission(commandGroup.Name, (int) Permissions.System))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                switch (wasGetEnumValueFromDescription<Action>(wasInput(
                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)), message))
                    .ToLowerInvariant()))
                {
                    case Action.PURGE:
                        Client.Assets.Cache.BeginPrune();
                        Cache.Purge();
                        break;
                    case Action.SAVE:
                        SaveCorradeCache.Invoke();
                        break;
                    case Action.LOAD:
                        LoadCorradeCache.Invoke();
                        break;
                    default:
                        throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                }
            };
        }
    }
}