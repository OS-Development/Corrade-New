///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfigurationSharp;
using wasSharp;
using Reflection = wasOpenMetaverse.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getconfigurationdata =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.System))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        var data = new List<string>();
                        lock (ConfigurationFileLock)
                        {
                            data.AddRange(Reflection.GetStructuredData(corradeConfiguration,
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(wasSharp.Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message)))
                            );
                        }
                        if (data.Any())
                            result.Add(wasSharp.Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(data));
                    };
        }
    }
}