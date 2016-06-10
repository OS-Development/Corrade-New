///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.ServiceModel.Syndication;
using System.Xml;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> feed =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Feed))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var url = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.URL)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(url))
                        throw new ScriptException(ScriptError.INVALID_URL_PROVIDED);
                    var name = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                        throw new ScriptException(ScriptError.NO_NAME_PROVIDED);
                    var action =
                        Reflection.GetEnumValueFromName<Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant());
                    // Check for valid URLs.
                    switch (action)
                    {
                        case Action.ADD:
                        case Action.REMOVE:
                            if (string.IsNullOrEmpty(url))
                                throw new ScriptException(ScriptError.INVALID_URL_PROVIDED);
                            break;
                    }
                    // Perform the operation.
                    switch (action)
                    {
                        case Action.ADD:
                            // Check whether the feed is valid before adding.
                            try
                            {
                                using (var reader = XmlReader.Create(url))
                                {
                                    SyndicationFeed.Load(reader);
                                }
                            }
                            catch (Exception)
                            {
                                throw new ScriptException(ScriptError.INVALID_FEED_PROVIDED);
                            }
                            // Add the feed.
                            lock (GroupFeedsLock)
                            {
                                if (GroupFeeds.ContainsKey(url))
                                {
                                    if (GroupFeeds[url].ContainsKey(corradeCommandParameters.Group.UUID))
                                    {
                                        throw new ScriptException(ScriptError.ALREADY_SUBSCRIBED_TO_FEED);
                                    }
                                    GroupFeeds[url].Add(corradeCommandParameters.Group.UUID, name);
                                    return;
                                }
                                GroupFeeds.Add(url, new Collections.SerializableDictionary<UUID, string>
                                {
                                    {corradeCommandParameters.Group.UUID, name}
                                });
                            }
                            break;
                        case Action.REMOVE:
                            lock (GroupFeedsLock)
                            {
                                // first remove the calling group.
                                if (!GroupFeeds.ContainsKey(url))
                                    return;
                                // in case there are no more subscribers to this feed
                                // then remove the feed altogether
                                GroupFeeds[url].Remove(corradeCommandParameters.Group.UUID);
                                if (!GroupFeeds[url].Any())
                                    GroupFeeds.Remove(url);
                            }
                            break;
                        case Action.LIST:
                            var csv = new List<string>();
                            lock (GroupFeedsLock)
                            {
                                var LockObject = new object();
                                GroupFeeds.AsParallel().ForAll(o =>
                                {
                                    string feedName;
                                    if (!o.Value.TryGetValue(corradeCommandParameters.Group.UUID, out feedName)) return;
                                    lock (LockObject)
                                    {
                                        csv.Add(feedName);
                                        csv.Add(o.Key);
                                    }
                                });
                            }
                            if (csv.Any())
                            {
                                result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                                    CSV.FromEnumerable(csv));
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                    // Save the state in case the feeds changed.
                    switch (action)
                    {
                        case Action.ADD:
                        case Action.REMOVE:
                            SaveGroupFeedState.Invoke();
                            break;
                    }
                };
        }
    }
}