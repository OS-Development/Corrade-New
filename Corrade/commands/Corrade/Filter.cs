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
using wasSharp.Collections.Specialized;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> filter =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Filter))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))))
                    {
                        case Enumerations.Action.SET:
                            var inputFilters = new ConcurrentList<Configuration.Filter>();
                            var input =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.INPUT)),
                                        corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(input))
                            {
                                foreach (var i in CSV.ToKeyValue(input).AsParallel()
                                    .GroupBy(o => o.Key)
                                    .Select(o => o.FirstOrDefault())
                                    .ToDictionary(o => wasInput(o.Key), o => wasInput(o.Value)))
                                {
                                    inputFilters.Add(Reflection.GetEnumValueFromName<Configuration.Filter>(i.Key));
                                    inputFilters.Add(Reflection.GetEnumValueFromName<Configuration.Filter>(i.Value));
                                }
                                lock (InputFiltersLock)
                                {
                                    corradeConfiguration.InputFilters = inputFilters;
                                }
                            }
                            var outputFilters = new ConcurrentList<Configuration.Filter>();
                            var output =
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.OUTPUT)),
                                    corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(output))
                            {
                                foreach (var i in CSV.ToKeyValue(output).AsParallel()
                                    .GroupBy(o => o.Key)
                                    .Select(o => o.FirstOrDefault())
                                    .ToDictionary(o => wasInput(o.Key), o => wasInput(o.Value)))
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

                        case Enumerations.Action.GET:
                            switch (Reflection.GetEnumValueFromName<Enumerations.Type>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                        corradeCommandParameters.Message))))
                            {
                                case Enumerations.Type.INPUT:
                                    lock (InputFiltersLock)
                                    {
                                        if (corradeConfiguration.InputFilters.Any())
                                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                                CSV.FromEnumerable(corradeConfiguration.InputFilters.Select(
                                                    o => Reflection.GetNameFromEnumValue(o))));
                                    }
                                    break;

                                case Enumerations.Type.OUTPUT:
                                    lock (OutputFiltersLock)
                                    {
                                        if (corradeConfiguration.OutputFilters.Any())
                                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                                CSV.FromEnumerable(corradeConfiguration.OutputFilters.Select(
                                                    o => Reflection.GetNameFromEnumValue(o))));
                                    }
                                    break;
                            }
                            break;
                    }
                };
        }
    }
}