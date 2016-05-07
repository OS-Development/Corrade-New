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
                    string url = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.URL)),
                            corradeCommandParameters.Message));
                    Action action =
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
                                using (XmlReader reader = XmlReader.Create(url))
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
                                    GroupFeeds[url].Add(corradeCommandParameters.Group.UUID);
                                    return;
                                }
                                GroupFeeds.Add(url,
                                    new HashSet<UUID> {corradeCommandParameters.Group.UUID});
                            }
                            break;
                        case Action.REMOVE:
                            lock (GroupFeedsLock)
                            {
                                if (!GroupFeeds.ContainsKey(url))
                                    return;
                                GroupFeeds[url].Remove(corradeCommandParameters.Group.UUID);
                            }
                            break;
                        case Action.LIST:
                            List<string> csv = new List<string>();
                            lock (GroupFeedsLock)
                            {
                                csv.AddRange(
                                    GroupFeeds.AsParallel()
                                        .Where(o => o.Value.Contains(corradeCommandParameters.Group.UUID))
                                        .Select(o => o.Key));
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
                            SaveFeedState.Invoke();
                            break;
                    }
                };
        }
    }
}