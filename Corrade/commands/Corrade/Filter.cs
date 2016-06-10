///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> filter =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Filter))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    switch (Reflection.GetEnumValueFromName<Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Action.SET:
                            var inputFilters = new List<Configuration.Filter>();
                            var input =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.INPUT)),
                                        corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(input))
                            {
                                foreach (var i in CSV.ToKeyValue(input))
                                {
                                    inputFilters.Add(Reflection.GetEnumValueFromName<Configuration.Filter>(i.Key));
                                    inputFilters.Add(Reflection.GetEnumValueFromName<Configuration.Filter>(i.Value));
                                }
                                lock (InputFiltersLock)
                                {
                                    corradeConfiguration.InputFilters = inputFilters;
                                }
                            }
                            var outputFilters = new List<Configuration.Filter>();
                            var output =
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.OUTPUT)),
                                    corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(output))
                            {
                                foreach (var i in CSV.ToKeyValue(output))
                                {
                                    outputFilters.Add(Reflection.GetEnumValueFromName<Configuration.Filter>(i.Key));
                                    outputFilters.Add(Reflection.GetEnumValueFromName<Configuration.Filter>(i.Value));
                                }
                                lock (OutputFiltersLock)
                                {
                                    corradeConfiguration.OutputFilters = outputFilters;
                                }
                            }
                            break;
                        case Action.GET:
                            switch (Reflection.GetEnumValueFromName<Type>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                                        corradeCommandParameters.Message)).ToLowerInvariant()))
                            {
                                case Type.INPUT:
                                    lock (InputFiltersLock)
                                    {
                                        if (corradeConfiguration.InputFilters.Any())
                                        {
                                            result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                                                CSV.FromEnumerable(corradeConfiguration.InputFilters.Select(
                                                    o => Reflection.GetNameFromEnumValue(o))));
                                        }
                                    }
                                    break;
                                case Type.OUTPUT:
                                    lock (OutputFiltersLock)
                                    {
                                        if (corradeConfiguration.OutputFilters.Any())
                                        {
                                            result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                                                CSV.FromEnumerable(corradeConfiguration.OutputFilters.Select(
                                                    o => Reflection.GetNameFromEnumValue(o))));
                                        }
                                    }
                                    break;
                            }
                            break;
                    }
                };
        }
    }
}