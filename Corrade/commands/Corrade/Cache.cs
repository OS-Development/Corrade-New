///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using CorradeConfiguration;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> cache =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.System))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    switch (Reflection.GetEnumValueFromName<Action>(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                            corradeCommandParameters.Message))
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