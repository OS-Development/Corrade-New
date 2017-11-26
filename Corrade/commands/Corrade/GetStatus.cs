///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getstatus =
                (corradeCommandParameters, result) =>
                {
                    uint status;
                    if (!uint.TryParse(wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.STATUS)),
                        corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture, out status))
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_STATUS_SUPPLIED);
                    switch (Reflection.GetEnumValueFromName<Enumerations.Entity>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message))))
                    {
                        case Enumerations.Entity.DESCRIPTION:
                            var scriptErrorFieldInfo = typeof(Enumerations.ScriptError).GetFields(
                                    BindingFlags.Public | BindingFlags.Static)
                                .AsParallel()
                                .FirstOrDefault(
                                    o =>
                                        Reflection.GetAttributeFromEnumValue<Command.StatusAttribute>(
                                                (Enumerations.ScriptError) o.GetValue(null))
                                            .Status.Equals(status));
                            if (scriptErrorFieldInfo == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.STATUS_NOT_FOUND);
                            var description =
                                Reflection.GetNameFromEnumValue(
                                    (Enumerations.ScriptError) scriptErrorFieldInfo.GetValue(null));
                            if (string.IsNullOrEmpty(description))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_DESCRIPTION_FOR_STATUS);
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), description);
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }
                };
        }
    }
}