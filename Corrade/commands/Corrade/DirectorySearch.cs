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

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> directorysearch =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Directory))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var DirectorySearchResultsAlarm =
                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType);
                    var name =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    var fields =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                            corradeCommandParameters.Message));
                    var LockObject = new object();
                    var csv = new List<string>();
                    var handledEvents = 0;
                    var counter = 1;
                    switch (
                        Reflection.GetEnumValueFromName<Type>(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                                corradeCommandParameters.Message))
                                .ToLowerInvariant()))
                    {
                        case Type.CLASSIFIED:
                            var searchClassified = new DirectoryManager.Classified();
                            wasCSVToStructure(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                        corradeCommandParameters.Message)),
                                ref searchClassified);
                            var classifieds =
                                new Dictionary<DirectoryManager.Classified, int>();
                            EventHandler<DirClassifiedsReplyEventArgs> DirClassifiedsEventHandler =
                                (sender, args) => args.Classifieds.AsParallel().ForAll(o =>
                                {
                                    DirectorySearchResultsAlarm.Alarm(corradeConfiguration.DataTimeout);
                                    var score = !string.IsNullOrEmpty(fields)
                                        ? wasGetFields(searchClassified, searchClassified.GetType().Name)
                                            .Sum(
                                                p =>
                                                    (from q in
                                                        wasGetFields(o,
                                                            o.GetType().Name)
                                                        let r = wasGetInfoValue(p.Key, p.Value)
                                                        where r != null
                                                        let s = wasGetInfoValue(q.Key, q.Value)
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
                                o => wasGetFields(o.Key, o.Key.GetType().Name).AsParallel().ForAll(p =>
                                {
                                    lock (LockObject)
                                    {
                                        csv.Add(p.Key.Name);
                                        csv.AddRange(wasGetInfo(p.Key, p.Value));
                                    }
                                }));
                            break;
                        case Type.EVENT:
                            var searchEvent = new DirectoryManager.EventsSearchData();
                            wasCSVToStructure(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                        corradeCommandParameters.Message)),
                                ref searchEvent);
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
                                            ? wasGetFields(searchEvent, searchEvent.GetType().Name)
                                                .Sum(
                                                    p =>
                                                        (from q in
                                                            wasGetFields(o, o.GetType().Name)
                                                            let r = wasGetInfoValue(p.Key, p.Value)
                                                            where r != null
                                                            let s = wasGetInfoValue(q.Key, q.Value)
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
                                    if (handledEvents > Constants.DIRECTORY.EVENT.SEARCH_RESULTS_COUNT &&
                                        ((handledEvents - counter)%
                                         Constants.DIRECTORY.EVENT.SEARCH_RESULTS_COUNT).Equals(0))
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
                                o => wasGetFields(o.Key, o.Key.GetType().Name).AsParallel().ForAll(p =>
                                {
                                    lock (LockObject)
                                    {
                                        csv.Add(p.Key.Name);
                                        csv.AddRange(wasGetInfo(p.Key, p.Value));
                                    }
                                }));
                            break;
                        case Type.GROUP:
                            if (string.IsNullOrEmpty(name))
                            {
                                throw new ScriptException(ScriptError.NO_SEARCH_TEXT_PROVIDED);
                            }
                            var searchGroup = new DirectoryManager.GroupSearchData();
                            wasCSVToStructure(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                        corradeCommandParameters.Message)),
                                ref searchGroup);
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
                                            ? wasGetFields(searchGroup, searchGroup.GetType().Name)
                                                .Sum(
                                                    p =>
                                                        (from q in
                                                            wasGetFields(o, o.GetType().Name)
                                                            let r = wasGetInfoValue(p.Key, p.Value)
                                                            where r != null
                                                            let s = wasGetInfoValue(q.Key, q.Value)
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
                                    if (handledEvents > Constants.DIRECTORY.GROUP.SEARCH_RESULTS_COUNT &&
                                        ((handledEvents - counter)%
                                         Constants.DIRECTORY.GROUP.SEARCH_RESULTS_COUNT).Equals(0))
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
                                o => wasGetFields(o.Key, o.Key.GetType().Name).AsParallel().ForAll(p =>
                                {
                                    lock (LockObject)
                                    {
                                        csv.Add(p.Key.Name);
                                        csv.AddRange(wasGetInfo(p.Key, p.Value));
                                    }
                                }));
                            break;
                        case Type.LAND:
                            var searchLand = new DirectoryManager.DirectoryParcel();
                            wasCSVToStructure(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                        corradeCommandParameters.Message)),
                                ref searchLand);
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
                                            ? wasGetFields(searchLand, searchLand.GetType().Name)
                                                .Sum(
                                                    p =>
                                                        (from q in
                                                            wasGetFields(o, o.GetType().Name)
                                                            let r = wasGetInfoValue(p.Key, p.Value)
                                                            where r != null
                                                            let s = wasGetInfoValue(q.Key, q.Value)
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
                                    if (handledEvents > Constants.DIRECTORY.LAND.SEARCH_RESULTS_COUNT &&
                                        ((handledEvents - counter)%
                                         Constants.DIRECTORY.LAND.SEARCH_RESULTS_COUNT).Equals(0))
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
                                o => wasGetFields(o.Key, o.Key.GetType().Name).AsParallel().ForAll(p =>
                                {
                                    lock (LockObject)
                                    {
                                        csv.Add(p.Key.Name);
                                        csv.AddRange(wasGetInfo(p.Key, p.Value));
                                    }
                                }));
                            break;
                        case Type.PEOPLE:
                            if (string.IsNullOrEmpty(name))
                            {
                                throw new ScriptException(ScriptError.NO_SEARCH_TEXT_PROVIDED);
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
                                            ? wasGetFields(searchAgent, searchAgent.GetType().Name)
                                                .Sum(
                                                    p =>
                                                        (from q in
                                                            wasGetFields(o, o.GetType().Name)
                                                            let r = wasGetInfoValue(p.Key, p.Value)
                                                            where r != null
                                                            let s = wasGetInfoValue(q.Key, q.Value)
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
                                    if (handledEvents > Constants.DIRECTORY.PEOPLE.SEARCH_RESULTS_COUNT &&
                                        ((handledEvents - counter)%
                                         Constants.DIRECTORY.PEOPLE.SEARCH_RESULTS_COUNT).Equals(0))
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
                                o => wasGetFields(o.Key, o.Key.GetType().Name).AsParallel().ForAll(p =>
                                {
                                    lock (LockObject)
                                    {
                                        csv.Add(p.Key.Name);
                                        csv.AddRange(wasGetInfo(p.Key, p.Value));
                                    }
                                }));
                            break;
                        case Type.PLACE:
                            if (string.IsNullOrEmpty(name))
                            {
                                throw new ScriptException(ScriptError.NO_SEARCH_TEXT_PROVIDED);
                            }
                            var searchPlaces = new DirectoryManager.PlacesSearchData();
                            wasCSVToStructure(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                        corradeCommandParameters.Message)),
                                ref searchPlaces);
                            var places =
                                new Dictionary<DirectoryManager.PlacesSearchData, int>();
                            EventHandler<PlacesReplyEventArgs> DirPlacesReplyEventHandler =
                                (sender, args) => args.MatchedPlaces.AsParallel().ForAll(o =>
                                {
                                    DirectorySearchResultsAlarm.Alarm(corradeConfiguration.DataTimeout);
                                    var score = !string.IsNullOrEmpty(fields)
                                        ? wasGetFields(searchPlaces, searchPlaces.GetType().Name)
                                            .Sum(
                                                p =>
                                                    (from q in
                                                        wasGetFields(o, o.GetType().Name)
                                                        let r = wasGetInfoValue(p.Key, p.Value)
                                                        where r != null
                                                        let s = wasGetInfoValue(q.Key, q.Value)
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
                                o => wasGetFields(o.Key, o.Key.GetType().Name).AsParallel().ForAll(p =>
                                {
                                    lock (LockObject)
                                    {
                                        csv.Add(p.Key.Name);
                                        csv.AddRange(wasGetInfo(p.Key, p.Value));
                                    }
                                }));
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_DIRECTORY_SEARCH_TYPE);
                    }
                    if (csv.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                    }
                };
        }
    }
}