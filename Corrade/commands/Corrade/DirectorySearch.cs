///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Timers;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> directorysearch
                =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Directory))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var DirectorySearchResultsAlarm =
                        new DecayingAlarm(corradeConfiguration.DataDecayType);
                    var name =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    var fields =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                            corradeCommandParameters.Message));
                    var LockObject = new object();
                    var csv = new List<string>();
                    var handledEvents = 0;
                    var counter = 1;
                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Type>(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                corradeCommandParameters.Message))
                                .ToLowerInvariant()))
                    {
                        case Enumerations.Type.CLASSIFIED:
                            var searchClassified = new DirectoryManager.Classified();
                            searchClassified = searchClassified.wasCSVToStructure(wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                    corradeCommandParameters.Message)));
                            var classifieds =
                                new Dictionary<DirectoryManager.Classified, int>();
                            EventHandler<DirClassifiedsReplyEventArgs> DirClassifiedsEventHandler =
                                (sender, args) => args.Classifieds.AsParallel().ForAll(o =>
                                {
                                    DirectorySearchResultsAlarm.Alarm(corradeConfiguration.DataTimeout);
                                    var score = !string.IsNullOrEmpty(fields)
                                        ? wasSharpNET.Reflection.wasGetFields(searchClassified,
                                            searchClassified.GetType().Name)
                                            .Sum(
                                                p =>
                                                    (from q in wasSharpNET.Reflection.wasGetFields(o,
                                                        o.GetType().Name)
                                                        let r = wasSharpNET.Reflection.wasGetInfoValue(p.Key, p.Value)
                                                        where r != null
                                                        let s = wasSharpNET.Reflection.wasGetInfoValue(q.Key, q.Value)
                                                        where s != null
                                                        where r.Equals(s)
                                                        select r).Count())
                                        : 0;
                                    lock (LockObject)
                                    {
                                        if (!classifieds.ContainsKey(o))
                                        {
                                            classifieds.Add(o, score);
                                        }
                                    }
                                });
                            lock (Locks.ClientInstanceDirectoryLock)
                            {
                                Client.Directory.DirClassifiedsReply += DirClassifiedsEventHandler;
                                Client.Directory.StartClassifiedSearch(name);
                                DirectorySearchResultsAlarm.Signal.WaitOne(
                                    (int) corradeConfiguration.ServicesTimeout,
                                    false);
                                Client.Directory.DirClassifiedsReply -= DirClassifiedsEventHandler;
                            }
                            Dictionary<DirectoryManager.Classified, int> safeClassifieds;
                            lock (LockObject)
                            {
                                safeClassifieds =
                                    classifieds.OrderByDescending(o => o.Value)
                                        .ToDictionary(o => o.Key, p => p.Value);
                            }
                            safeClassifieds.AsParallel().ForAll(
                                o =>
                                    wasSharpNET.Reflection.wasGetFields(o.Key, o.Key.GetType().Name)
                                        .AsParallel()
                                        .ForAll(p =>
                                        {
                                            lock (LockObject)
                                            {
                                                csv.Add(p.Key.Name);
                                                csv.AddRange(
                                                    wasOpenMetaverse.Reflection.wasSerializeObject(
                                                        wasSharpNET.Reflection.wasGetInfoValue(p.Key, p.Value)));
                                            }
                                        }));
                            break;
                        case Enumerations.Type.EVENT:
                            var searchEvent = new DirectoryManager.EventsSearchData();
                            searchEvent = searchEvent.wasCSVToStructure(wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                    corradeCommandParameters.Message)));
                            var events =
                                new Dictionary<DirectoryManager.EventsSearchData, int>();
                            EventHandler<DirEventsReplyEventArgs> DirEventsEventHandler =
                                (sender, args) =>
                                {
                                    DirectorySearchResultsAlarm.Alarm(corradeConfiguration.DataTimeout);
                                    handledEvents += args.MatchedEvents.Count;
                                    args.MatchedEvents.AsParallel().ForAll(o =>
                                    {
                                        var score = !string.IsNullOrEmpty(fields)
                                            ? wasSharpNET.Reflection.wasGetFields(searchEvent,
                                                searchEvent.GetType().Name)
                                                .Sum(
                                                    p =>
                                                        (from q in
                                                            wasSharpNET.Reflection.wasGetFields(o, o.GetType().Name)
                                                            let r =
                                                                wasSharpNET.Reflection.wasGetInfoValue(p.Key, p.Value)
                                                            where r != null
                                                            let s =
                                                                wasSharpNET.Reflection.wasGetInfoValue(q.Key, q.Value)
                                                            where s != null
                                                            where r.Equals(s)
                                                            select r).Count())
                                            : 0;
                                        lock (LockObject)
                                        {
                                            if (!events.ContainsKey(o))
                                            {
                                                events.Add(o, score);
                                            }
                                        }
                                    });
                                    if (handledEvents > wasOpenMetaverse.Constants.DIRECTORY.EVENT.SEARCH_RESULTS_COUNT &&
                                        ((handledEvents - counter)%
                                         wasOpenMetaverse.Constants.DIRECTORY.EVENT.SEARCH_RESULTS_COUNT).Equals(0))
                                    {
                                        ++counter;
                                        Client.Directory.StartEventsSearch(name, (uint) handledEvents);
                                    }
                                };
                            lock (Locks.ClientInstanceDirectoryLock)
                            {
                                Client.Directory.DirEventsReply += DirEventsEventHandler;
                                Client.Directory.StartEventsSearch(name,
                                    (uint) handledEvents);
                                DirectorySearchResultsAlarm.Signal.WaitOne(
                                    (int) corradeConfiguration.ServicesTimeout,
                                    false);
                                Client.Directory.DirEventsReply -= DirEventsEventHandler;
                            }
                            Dictionary<DirectoryManager.EventsSearchData, int> safeEvents;
                            lock (LockObject)
                            {
                                safeEvents = events.OrderByDescending(o => o.Value)
                                    .ToDictionary(o => o.Key, p => p.Value);
                            }
                            safeEvents.AsParallel().ForAll(
                                o =>
                                    wasSharpNET.Reflection.wasGetFields(o.Key, o.Key.GetType().Name)
                                        .AsParallel()
                                        .ForAll(p =>
                                        {
                                            lock (LockObject)
                                            {
                                                csv.Add(p.Key.Name);
                                                csv.AddRange(
                                                    wasOpenMetaverse.Reflection.wasSerializeObject(
                                                        wasSharpNET.Reflection.wasGetInfoValue(p.Key, p.Value)));
                                            }
                                        }));
                            break;
                        case Enumerations.Type.GROUP:
                            if (string.IsNullOrEmpty(name))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_SEARCH_TEXT_PROVIDED);
                            }
                            var searchGroup = new DirectoryManager.GroupSearchData();
                            searchGroup = searchGroup.wasCSVToStructure(wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                    corradeCommandParameters.Message)));
                            var groups =
                                new Dictionary<DirectoryManager.GroupSearchData, int>();
                            EventHandler<DirGroupsReplyEventArgs> DirGroupsEventHandler =
                                (sender, args) =>
                                {
                                    DirectorySearchResultsAlarm.Alarm(corradeConfiguration.DataTimeout);
                                    handledEvents += args.MatchedGroups.Count;
                                    args.MatchedGroups.AsParallel().ForAll(o =>
                                    {
                                        var score = !string.IsNullOrEmpty(fields)
                                            ? wasSharpNET.Reflection.wasGetFields(searchGroup,
                                                searchGroup.GetType().Name)
                                                .Sum(
                                                    p =>
                                                        (from q in
                                                            wasSharpNET.Reflection.wasGetFields(o, o.GetType().Name)
                                                            let r =
                                                                wasSharpNET.Reflection.wasGetInfoValue(p.Key, p.Value)
                                                            where r != null
                                                            let s =
                                                                wasSharpNET.Reflection.wasGetInfoValue(q.Key, q.Value)
                                                            where s != null
                                                            where r.Equals(s)
                                                            select r).Count())
                                            : 0;
                                        lock (LockObject)
                                        {
                                            if (!groups.ContainsKey(o))
                                            {
                                                groups.Add(o, score);
                                            }
                                        }
                                    });
                                    if (handledEvents > wasOpenMetaverse.Constants.DIRECTORY.GROUP.SEARCH_RESULTS_COUNT &&
                                        ((handledEvents - counter)%
                                         wasOpenMetaverse.Constants.DIRECTORY.GROUP.SEARCH_RESULTS_COUNT).Equals(0))
                                    {
                                        ++counter;
                                        Client.Directory.StartGroupSearch(name, handledEvents);
                                    }
                                };
                            lock (Locks.ClientInstanceDirectoryLock)
                            {
                                Client.Directory.DirGroupsReply += DirGroupsEventHandler;
                                Client.Directory.StartGroupSearch(name, handledEvents);
                                DirectorySearchResultsAlarm.Signal.WaitOne(
                                    (int) corradeConfiguration.ServicesTimeout,
                                    false);
                                Client.Directory.DirGroupsReply -= DirGroupsEventHandler;
                            }
                            Dictionary<DirectoryManager.GroupSearchData, int> safeGroups;
                            lock (LockObject)
                            {
                                safeGroups = groups.OrderByDescending(o => o.Value)
                                    .ToDictionary(o => o.Key, p => p.Value);
                            }
                            safeGroups.AsParallel().ForAll(
                                o =>
                                    wasSharpNET.Reflection.wasGetFields(o.Key, o.Key.GetType().Name)
                                        .AsParallel()
                                        .ForAll(p =>
                                        {
                                            lock (LockObject)
                                            {
                                                csv.Add(p.Key.Name);
                                                csv.AddRange(
                                                    wasOpenMetaverse.Reflection.wasSerializeObject(
                                                        wasSharpNET.Reflection.wasGetInfoValue(p.Key, p.Value)));
                                            }
                                        }));
                            break;
                        case Enumerations.Type.LAND:
                            var searchLand = new DirectoryManager.DirectoryParcel();
                            searchLand = searchLand.wasCSVToStructure(wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                    corradeCommandParameters.Message)));
                            var lands =
                                new Dictionary<DirectoryManager.DirectoryParcel, int>();
                            EventHandler<DirLandReplyEventArgs> DirLandReplyEventArgs =
                                (sender, args) =>
                                {
                                    DirectorySearchResultsAlarm.Alarm(corradeConfiguration.DataTimeout);
                                    handledEvents += args.DirParcels.Count;
                                    args.DirParcels.AsParallel().ForAll(o =>
                                    {
                                        var score = !string.IsNullOrEmpty(fields)
                                            ? wasSharpNET.Reflection.wasGetFields(searchLand, searchLand.GetType().Name)
                                                .Sum(
                                                    p =>
                                                        (from q in
                                                            wasSharpNET.Reflection.wasGetFields(o, o.GetType().Name)
                                                            let r =
                                                                wasSharpNET.Reflection.wasGetInfoValue(p.Key, p.Value)
                                                            where r != null
                                                            let s =
                                                                wasSharpNET.Reflection.wasGetInfoValue(q.Key, q.Value)
                                                            where s != null
                                                            where r.Equals(s)
                                                            select r).Count())
                                            : 0;
                                        lock (LockObject)
                                        {
                                            if (!lands.ContainsKey(o))
                                            {
                                                lands.Add(o, score);
                                            }
                                        }
                                    });
                                    if (handledEvents > wasOpenMetaverse.Constants.DIRECTORY.LAND.SEARCH_RESULTS_COUNT &&
                                        ((handledEvents - counter)%
                                         wasOpenMetaverse.Constants.DIRECTORY.LAND.SEARCH_RESULTS_COUNT).Equals(0))
                                    {
                                        ++counter;
                                        Client.Directory.StartLandSearch(DirectoryManager.DirFindFlags.SortAsc,
                                            DirectoryManager.SearchTypeFlags.Any, int.MaxValue, int.MaxValue,
                                            handledEvents);
                                    }
                                };
                            lock (Locks.ClientInstanceDirectoryLock)
                            {
                                Client.Directory.DirLandReply += DirLandReplyEventArgs;
                                Client.Directory.StartLandSearch(DirectoryManager.DirFindFlags.SortAsc,
                                    DirectoryManager.SearchTypeFlags.Any, int.MaxValue, int.MaxValue, handledEvents);
                                DirectorySearchResultsAlarm.Signal.WaitOne(
                                    (int) corradeConfiguration.ServicesTimeout,
                                    false);
                                Client.Directory.DirLandReply -= DirLandReplyEventArgs;
                            }
                            Dictionary<DirectoryManager.DirectoryParcel, int> safeLands;
                            lock (LockObject)
                            {
                                safeLands = lands.OrderByDescending(o => o.Value)
                                    .ToDictionary(o => o.Key, p => p.Value);
                            }
                            safeLands.AsParallel().ForAll(
                                o =>
                                    wasSharpNET.Reflection.wasGetFields(o.Key, o.Key.GetType().Name)
                                        .AsParallel()
                                        .ForAll(p =>
                                        {
                                            lock (LockObject)
                                            {
                                                csv.Add(p.Key.Name);
                                                csv.AddRange(
                                                    wasOpenMetaverse.Reflection.wasSerializeObject(
                                                        wasSharpNET.Reflection.wasGetInfoValue(p.Key, p.Value)));
                                            }
                                        }));
                            break;
                        case Enumerations.Type.PEOPLE:
                            if (string.IsNullOrEmpty(name))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_SEARCH_TEXT_PROVIDED);
                            }
                            var searchAgent = new DirectoryManager.AgentSearchData();
                            var agents =
                                new Dictionary<DirectoryManager.AgentSearchData, int>();
                            EventHandler<DirPeopleReplyEventArgs> DirPeopleReplyEventHandler =
                                (sender, args) =>
                                {
                                    DirectorySearchResultsAlarm.Alarm(corradeConfiguration.DataTimeout);
                                    handledEvents += args.MatchedPeople.Count;
                                    args.MatchedPeople.AsParallel().ForAll(o =>
                                    {
                                        var score = !string.IsNullOrEmpty(fields)
                                            ? wasSharpNET.Reflection.wasGetFields(searchAgent,
                                                searchAgent.GetType().Name)
                                                .Sum(
                                                    p =>
                                                        (from q in
                                                            wasSharpNET.Reflection.wasGetFields(o, o.GetType().Name)
                                                            let r =
                                                                wasSharpNET.Reflection.wasGetInfoValue(p.Key, p.Value)
                                                            where r != null
                                                            let s =
                                                                wasSharpNET.Reflection.wasGetInfoValue(q.Key, q.Value)
                                                            where s != null
                                                            where r.Equals(s)
                                                            select r).Count())
                                            : 0;
                                        lock (LockObject)
                                        {
                                            if (!agents.ContainsKey(o))
                                            {
                                                agents.Add(o, score);
                                            }
                                        }
                                    });
                                    if (handledEvents > wasOpenMetaverse.Constants.DIRECTORY.PEOPLE.SEARCH_RESULTS_COUNT &&
                                        ((handledEvents - counter)%
                                         wasOpenMetaverse.Constants.DIRECTORY.PEOPLE.SEARCH_RESULTS_COUNT).Equals(0))
                                    {
                                        ++counter;
                                        Client.Directory.StartPeopleSearch(name, handledEvents);
                                    }
                                };
                            lock (Locks.ClientInstanceDirectoryLock)
                            {
                                Client.Directory.DirPeopleReply += DirPeopleReplyEventHandler;
                                Client.Directory.StartPeopleSearch(name, handledEvents);
                                DirectorySearchResultsAlarm.Signal.WaitOne(
                                    (int) corradeConfiguration.ServicesTimeout,
                                    false);
                                Client.Directory.DirPeopleReply -= DirPeopleReplyEventHandler;
                            }
                            Dictionary<DirectoryManager.AgentSearchData, int> safeAgents;
                            lock (LockObject)
                            {
                                safeAgents = agents.OrderByDescending(o => o.Value)
                                    .ToDictionary(o => o.Key, p => p.Value);
                            }
                            safeAgents.AsParallel().ForAll(
                                o =>
                                    wasSharpNET.Reflection.wasGetFields(o.Key, o.Key.GetType().Name)
                                        .AsParallel()
                                        .ForAll(p =>
                                        {
                                            lock (LockObject)
                                            {
                                                csv.Add(p.Key.Name);
                                                csv.AddRange(
                                                    wasOpenMetaverse.Reflection.wasSerializeObject(
                                                        wasSharpNET.Reflection.wasGetInfoValue(p.Key, p.Value)));
                                            }
                                        }));
                            break;
                        case Enumerations.Type.PLACE:
                            if (string.IsNullOrEmpty(name))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_SEARCH_TEXT_PROVIDED);
                            }
                            var searchPlaces = new DirectoryManager.PlacesSearchData();
                            searchPlaces = searchPlaces.wasCSVToStructure(wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                    corradeCommandParameters.Message)));
                            var places =
                                new Dictionary<DirectoryManager.PlacesSearchData, int>();
                            EventHandler<PlacesReplyEventArgs> DirPlacesReplyEventHandler =
                                (sender, args) => args.MatchedPlaces.AsParallel().ForAll(o =>
                                {
                                    DirectorySearchResultsAlarm.Alarm(corradeConfiguration.DataTimeout);
                                    var score = !string.IsNullOrEmpty(fields)
                                        ? wasSharpNET.Reflection.wasGetFields(searchPlaces, searchPlaces.GetType().Name)
                                            .Sum(
                                                p =>
                                                    (from q in wasSharpNET.Reflection.wasGetFields(o, o.GetType().Name)
                                                        let r = wasSharpNET.Reflection.wasGetInfoValue(p.Key, p.Value)
                                                        where r != null
                                                        let s = wasSharpNET.Reflection.wasGetInfoValue(q.Key, q.Value)
                                                        where s != null
                                                        where r.Equals(s)
                                                        select r).Count())
                                        : 0;
                                    lock (LockObject)
                                    {
                                        if (!places.ContainsKey(o))
                                        {
                                            places.Add(o, score);
                                        }
                                    }
                                });
                            lock (Locks.ClientInstanceDirectoryLock)
                            {
                                Client.Directory.PlacesReply += DirPlacesReplyEventHandler;
                                Client.Directory.StartPlacesSearch(name);
                                DirectorySearchResultsAlarm.Signal.WaitOne(
                                    (int) corradeConfiguration.ServicesTimeout,
                                    false);
                                Client.Directory.PlacesReply -= DirPlacesReplyEventHandler;
                            }
                            Dictionary<DirectoryManager.PlacesSearchData, int> safePlaces;
                            lock (LockObject)
                            {
                                safePlaces = places.OrderByDescending(o => o.Value)
                                    .ToDictionary(o => o.Key, p => p.Value);
                            }
                            safePlaces.AsParallel().ForAll(
                                o =>
                                    wasSharpNET.Reflection.wasGetFields(o.Key, o.Key.GetType().Name)
                                        .AsParallel()
                                        .ForAll(p =>
                                        {
                                            lock (LockObject)
                                            {
                                                csv.Add(p.Key.Name);
                                                csv.AddRange(
                                                    wasOpenMetaverse.Reflection.wasSerializeObject(
                                                        wasSharpNET.Reflection.wasGetInfoValue(p.Key, p.Value)));
                                            }
                                        }));
                            break;
                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_DIRECTORY_SEARCH_TYPE);
                    }
                    if (csv.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                    }
                };
        }
    }
}