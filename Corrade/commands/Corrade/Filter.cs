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
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Filter))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    switch (Reflection.wasGetEnumValueFromName<Action>(
                        wasInput(
                            KeyValue.wasKeyValueGet(wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Action.SET:
                            List<Configuration.Filter> inputFilters = new List<Configuration.Filter>();
                            string input =
                                wasInput(
                                    KeyValue.wasKeyValueGet(
                                        wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.INPUT)),
                                        corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(input))
                            {
                                foreach (
                                    KeyValuePair<string, string> i in
                                        CSV.wasCSVToEnumerable(input).AsParallel().Select((o, p) => new {o, p})
                                            .GroupBy(q => q.p/2, q => q.o)
                                            .Select(o => o.ToList())
                                            .TakeWhile(o => o.Count%2 == 0)
                                            .Where(
                                                o =>
                                                    !string.IsNullOrEmpty(o.First()) ||
                                                    !string.IsNullOrEmpty(o.Last()))
                                            .ToDictionary(o => o.First(), p => p.Last()))
                                {
                                    inputFilters.Add(Reflection.wasGetEnumValueFromName<Configuration.Filter>(i.Key));
                                    inputFilters.Add(Reflection.wasGetEnumValueFromName<Configuration.Filter>(i.Value));
                                }
                                lock (InputFiltersLock)
                                {
                                    corradeConfiguration.InputFilters = inputFilters;
                                }
                            }
                            List<Configuration.Filter> outputFilters = new List<Configuration.Filter>();
                            string output =
                                wasInput(KeyValue.wasKeyValueGet(
                                    wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.OUTPUT)),
                                    corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(output))
                            {
                                foreach (
                                    KeyValuePair<string, string> i in
                                        CSV.wasCSVToEnumerable(output).AsParallel().Select((o, p) => new {o, p})
                                            .GroupBy(q => q.p/2, q => q.o)
                                            .Select(o => o.ToList())
                                            .TakeWhile(o => o.Count%2 == 0)
                                            .Where(
                                                o =>
                                                    !string.IsNullOrEmpty(o.First()) ||
                                                    !string.IsNullOrEmpty(o.Last()))
                                            .ToDictionary(o => o.First(), p => p.Last()))
                                {
                                    outputFilters.Add(Reflection.wasGetEnumValueFromName<Configuration.Filter>(i.Key));
                                    outputFilters.Add(Reflection.wasGetEnumValueFromName<Configuration.Filter>(i.Value));
                                }
                                lock (OutputFiltersLock)
                                {
                                    corradeConfiguration.OutputFilters = outputFilters;
                                }
                            }
                            break;
                        case Action.GET:
                            switch (Reflection.wasGetEnumValueFromName<Type>(
                                wasInput(
                                    KeyValue.wasKeyValueGet(
                                        wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.TYPE)),
                                        corradeCommandParameters.Message)).ToLowerInvariant()))
                            {
                                case Type.INPUT:
                                    lock (InputFiltersLock)
                                    {
                                        if (corradeConfiguration.InputFilters.Any())
                                        {
                                            result.Add(Reflection.wasGetNameFromEnumValue(ResultKeys.DATA),
                                                CSV.wasEnumerableToCSV(corradeConfiguration.InputFilters.Select(
                                                    o => Reflection.wasGetNameFromEnumValue(o))));
                                        }
                                    }
                                    break;
                                case Type.OUTPUT:
                                    lock (OutputFiltersLock)
                                    {
                                        if (corradeConfiguration.OutputFilters.Any())
                                        {
                                            result.Add(Reflection.wasGetNameFromEnumValue(ResultKeys.DATA),
                                                CSV.wasEnumerableToCSV(corradeConfiguration.OutputFilters.Select(
                                                    o => Reflection.wasGetNameFromEnumValue(o))));
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