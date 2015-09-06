///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> filter = (commandGroup, message, result) =>
            {
                if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Filter))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                switch (wasGetEnumValueFromDescription<Action>(
                    wasInput(
                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)),
                            message)).ToLowerInvariant()))
                {
                    case Action.SET:
                        List<Filter> inputFilters = new List<Filter>();
                        string input =
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.INPUT)),
                                message));
                        if (!string.IsNullOrEmpty(input))
                        {
                            foreach (
                                KeyValuePair<string, string> i in
                                    wasCSVToEnumerable(input).AsParallel().Select((o, p) => new {o, p})
                                        .GroupBy(q => q.p/2, q => q.o)
                                        .Select(o => o.ToList())
                                        .TakeWhile(o => o.Count%2 == 0)
                                        .Where(
                                            o =>
                                                !string.IsNullOrEmpty(o.First()) ||
                                                !string.IsNullOrEmpty(o.Last()))
                                        .ToDictionary(o => o.First(), p => p.Last()))
                            {
                                inputFilters.Add(wasGetEnumValueFromDescription<Filter>(i.Key));
                                inputFilters.Add(wasGetEnumValueFromDescription<Filter>(i.Value));
                            }
                            lock (InputFiltersLock)
                            {
                                corradeConfiguration.InputFilters = inputFilters;
                            }
                        }
                        List<Filter> outputFilters = new List<Filter>();
                        string output =
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.OUTPUT)),
                                message));
                        if (!string.IsNullOrEmpty(output))
                        {
                            foreach (
                                KeyValuePair<string, string> i in
                                    wasCSVToEnumerable(output).AsParallel().Select((o, p) => new {o, p})
                                        .GroupBy(q => q.p/2, q => q.o)
                                        .Select(o => o.ToList())
                                        .TakeWhile(o => o.Count%2 == 0)
                                        .Where(
                                            o =>
                                                !string.IsNullOrEmpty(o.First()) ||
                                                !string.IsNullOrEmpty(o.Last()))
                                        .ToDictionary(o => o.First(), p => p.Last()))
                            {
                                outputFilters.Add(wasGetEnumValueFromDescription<Filter>(i.Key));
                                outputFilters.Add(wasGetEnumValueFromDescription<Filter>(i.Value));
                            }
                            lock (OutputFiltersLock)
                            {
                                corradeConfiguration.OutputFilters = outputFilters;
                            }
                        }
                        break;
                    case Action.GET:
                        switch (wasGetEnumValueFromDescription<Type>(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)),
                                    message)).ToLowerInvariant()))
                        {
                            case Type.INPUT:
                                lock (InputFiltersLock)
                                {
                                    if (corradeConfiguration.InputFilters.Any())
                                    {
                                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                                            wasEnumerableToCSV(corradeConfiguration.InputFilters.Select(
                                                o => wasGetDescriptionFromEnumValue(o))));
                                    }
                                }
                                break;
                            case Type.OUTPUT:
                                lock (OutputFiltersLock)
                                {
                                    if (corradeConfiguration.OutputFilters.Any())
                                    {
                                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                                            wasEnumerableToCSV(corradeConfiguration.OutputFilters.Select(
                                                o => wasGetDescriptionFromEnumValue(o))));
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