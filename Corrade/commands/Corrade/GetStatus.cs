///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> getstatus =
                (commandGroup, message, result) =>
                {
                    uint status;
                    if (!uint.TryParse(wasInput(wasKeyValueGet(
                        wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.STATUS)),
                        message)), out status))
                    {
                        throw new ScriptException(ScriptError.INVALID_STATUS_SUPPLIED);
                    }
                    switch (wasGetEnumValueFromDescription<Entity>(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ENTITY)),
                                message)).ToLowerInvariant()))
                    {
                        case Entity.DESCRIPTION:
                            FieldInfo scriptErrorFieldInfo = typeof (ScriptError).GetFields(
                                BindingFlags.Public | BindingFlags.Static)
                                .AsParallel()
                                .FirstOrDefault(
                                    o =>
                                        wasGetAttributeFromEnumValue<StatusAttribute>((ScriptError) o.GetValue(null))
                                            .Status.Equals(status));
                            if (scriptErrorFieldInfo == null)
                                throw new ScriptException(ScriptError.STATUS_NOT_FOUND);
                            string description =
                                wasGetDescriptionFromEnumValue((ScriptError) scriptErrorFieldInfo.GetValue(null));
                            if (string.IsNullOrEmpty(description))
                                throw new ScriptException(ScriptError.NO_DESCRIPTION_FOR_STATUS);
                            result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA), description);
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                    }
                };
        }
    }
}