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
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasSharp;
using wasSharp.Collections.Generic;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> feed =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Feed))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var action =
                        Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))
                        );
                    // Check for passed parameters.
                    var name = string.Empty;
                    var url = string.Empty;
                    switch (action)
                    {
                        case Enumerations.Action.ADD:
                            name = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(name))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);
                            goto case Enumerations.Action.REMOVE;
                        case Enumerations.Action.REMOVE:
                            url = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.URL)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(url))
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_URL_PROVIDED);
                            break;
                    }
                    // Perform the operation.
                    switch (action)
                    {
                        case Enumerations.Action.ADD:
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
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_FEED_PROVIDED);
                            }
                            // Add the feed.
                            lock (GroupFeedsLock)
                            {
                                if (GroupFeeds.ContainsKey(url))
                                {
                                    if (GroupFeeds[url].ContainsKey(corradeCommandParameters.Group.UUID))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.ALREADY_SUBSCRIBED_TO_FEED);
                                    GroupFeeds[url].Add(corradeCommandParameters.Group.UUID, name);
                                    return;
                                }
                                GroupFeeds.Add(url, new SerializableDictionary<UUID, string>
                                {
                                    {corradeCommandParameters.Group.UUID, name}
                                });
                            }
                            break;

                        case Enumerations.Action.REMOVE:
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

                        case Enumerations.Action.LIST:
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
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                    CSV.FromEnumerable(csv));
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                    // Save the state in case the feeds changed.
                    switch (action)
                    {
                        case Enumerations.Action.ADD:
                        case Enumerations.Action.REMOVE:
                            SaveGroupFeedState.Invoke();
                            break;
                    }
                };
        }
    }
}