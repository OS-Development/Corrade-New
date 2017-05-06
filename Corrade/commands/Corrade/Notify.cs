///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using CorradeConfigurationSharp;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using wasSharp;
using wasSharp.Collections.Generic;
using wasSharp.Linq;
using wasSharp.Web.Utilities;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> notify =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int)Configuration.Permissions.Notifications))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var tag = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TAG)),
                            corradeCommandParameters.Message));
                    var url = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.URL)),
                            corradeCommandParameters.Message));
                    var notificationTypes =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                corradeCommandParameters.Message));
                    var action =
                        Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))
                            );
                    var LockObject = new object();
                    var groupNotifications = new HashSet<Notifications>();
                    var tags = new HashSet<string>(CSV.ToEnumerable(tag)
                        .Where(o => !string.IsNullOrEmpty(o)).Distinct());
                    switch (action)
                    {
                        case Enumerations.Action.SET:
                        case Enumerations.Action.ADD:
                            if (string.IsNullOrEmpty(url))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_URL_PROVIDED);
                            }
                            Uri notifyURL;
                            if (!Uri.TryCreate(url, UriKind.Absolute, out notifyURL))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_URL_PROVIDED);
                            }
                            if (string.IsNullOrEmpty(notificationTypes))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_NOTIFICATION_TYPES);
                            }
                            Notifications notification;
                            lock (GroupNotificationsLock)
                            {
                                notification =
                                    GroupNotifications.AsParallel().FirstOrDefault(
                                        o =>
                                            o.GroupUUID.Equals(corradeCommandParameters.Group.UUID));
                            }
                            // Get any afterburn data.
                            var afterBurnData =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AFTERBURN)),
                                        corradeCommandParameters.Message));
                            var afterburn =
                                new SerializableDictionary<string, string>();
                            if (!string.IsNullOrEmpty(afterBurnData))
                            {
                                // remove keys that are script keys, result keys or invalid key-value pairs
                                var results = new HashSet<string>(Reflection.GetEnumNames<Command.ResultKeys>());
                                var scripts = new HashSet<string>(Reflection.GetEnumNames<Command.ScriptKeys>());
                                CSV.ToKeyValue(afterBurnData)
                                    .ToDictionary(o => wasInput(o.Key), o => wasInput(o.Value))
                                    .AsParallel()
                                    .Where(
                                        o =>
                                            !string.IsNullOrEmpty(o.Key) && !results.Contains(o.Key) &&
                                            !string.IsNullOrEmpty(o.Value) && !scripts.Contains(o.Key))
                                    .ForAll(
                                        o =>
                                        {
                                            lock (LockObject)
                                            {
                                                afterburn.Add(o.Key, o.Value);
                                            }
                                        });
                            }
                            // Build any requested data for raw notifications.
                            var fields =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message));
                            var data = new HashSet<string>();
                            if (!string.IsNullOrEmpty(fields))
                            {
                                CSV.ToEnumerable(fields)
                                    .AsParallel()
                                    .Where(o => !string.IsNullOrEmpty(o))
                                    .ForAll(
                                        o =>
                                        {
                                            lock (LockObject)
                                            {
                                                data.Add(o);
                                            }
                                        });
                            }
                            switch (notification != null)
                            {
                                case false:
                                    notification = new Notifications
                                    {
                                        GroupName = corradeCommandParameters.Group.Name,
                                        GroupUUID = corradeCommandParameters.Group.UUID,
                                        HTTPNotifications =
                                            new SerializableDictionary<Configuration.Notifications,
                                                SerializableDictionary<string, HashSet<string>>>(),
                                        NotificationTCPDestination =
                                            new Dictionary<Configuration.Notifications, HashSet<IPEndPoint>>(),
                                        Data = data,
                                        Afterburn = afterburn
                                    };
                                    break;

                                case true:
                                    if (notification.NotificationTCPDestination == null)
                                    {
                                        notification.NotificationTCPDestination =
                                            new Dictionary<Configuration.Notifications, HashSet<IPEndPoint>>();
                                    }
                                    if (notification.HTTPNotifications == null)
                                    {
                                        notification.HTTPNotifications =
                                            new SerializableDictionary<Configuration.Notifications,
                                                SerializableDictionary<string, HashSet<string>>>();
                                    }
                                    break;
                            }
                            var scriptError = Enumerations.ScriptError.NONE;
                            Parallel.ForEach(CSV.ToEnumerable(notificationTypes).AsParallel().Where(o => !string.IsNullOrEmpty(o)),
                                (o, state) =>
                                {
                                    var notificationValue =
                                        (ulong)Reflection.GetEnumValueFromName<Configuration.Notifications>(o);
                                    if (!GroupHasNotification(corradeCommandParameters.Group.UUID, notificationValue))
                                    {
                                        // one of the notification was not allowed, so abort
                                        scriptError = Enumerations.ScriptError.NOTIFICATION_NOT_ALLOWED;
                                        state.Break();
                                    }
                                    notification.Data = data;
                                    notification.Afterburn = afterburn;
                                    SerializableDictionary<string, HashSet<string>> HTTPNotificationData;
                                    switch (!notification.HTTPNotifications
                                        .TryGetValue((Configuration.Notifications)notificationValue, out HTTPNotificationData))
                                    {
                                        case true:
                                            lock (LockObject)
                                            {
                                                notification.HTTPNotifications.Add(
                                                    (Configuration.Notifications)notificationValue,
                                                    new SerializableDictionary<string, HashSet<string>>
                                                        {
                                                            { url, tags }
                                                        }
                                                    );
                                            }
                                            break;

                                        default:
                                            switch (action)
                                            {
                                                case Enumerations.Action.ADD:
                                                    if (HTTPNotificationData.ContainsKey(url))
                                                        break;
                                                    HTTPNotificationData.Add(url, tags);
                                                    break;

                                                case Enumerations.Action.SET:
                                                    HTTPNotificationData =
                                                        new SerializableDictionary<string, HashSet<string>>
                                                    {
                                                            { url, tags }
                                                    };
                                                    break;
                                            }
                                            lock (LockObject)
                                            {
                                                notification.HTTPNotifications[
                                                    (Configuration.Notifications)notificationValue] = HTTPNotificationData;
                                            }
                                            break;
                                    }
                                });

                            if (!scriptError.Equals(Enumerations.ScriptError.NONE))
                                throw new Command.ScriptException(scriptError);

                            lock (GroupNotificationsLock)
                            {
                                // Replace notification.
                                GroupNotifications.RemoveWhere(
                                    o =>
                                        o.GroupUUID.Equals(corradeCommandParameters.Group.UUID));
                                GroupNotifications.Add(notification);
                            }
                            // Save the notifications state.
                            SaveNotificationState.Invoke();
                            break;

                        case Enumerations.Action.REMOVE:
                            lock (GroupNotificationsLock)
                            {
                                GroupNotifications.AsParallel().ForAll(o =>
                                {
                                    // Add all notitifcations that do not belong to the current group.
                                    if (!o.GroupUUID.Equals(corradeCommandParameters.Group.UUID))
                                    {
                                        lock (LockObject)
                                        {
                                            groupNotifications.Add(o);
                                        }
                                        return;
                                    }
                                    var notificationDestination =
                                        new SerializableDictionary<Configuration.Notifications,
                                            SerializableDictionary<string, HashSet<string>>>();
                                    var NotificationDestinationLock = new object();
                                    var types = new HashSet<Configuration.Notifications>(CSV.ToEnumerable(notificationTypes)
                                        .AsParallel()
                                        .Where(p => !string.IsNullOrEmpty(p))
                                        .Select(p => Reflection.GetEnumValueFromName<Configuration.Notifications>(p)));
                                    // For all notifications, for the current group, that do not match the supplied tags...
                                    o.HTTPNotifications
                                        .Where(p => !p.Value.Values.Any(q => q.Intersect(tags).Any()) && !types.Contains(p.Key))
                                        .AsParallel().ForAll(p =>
                                    {
                                        switch (!CSV.ToEnumerable(notificationTypes)
                                            .AsParallel()
                                            .Where(q => !string.IsNullOrEmpty(q))
                                            .Any(q => Reflection.GetEnumValueFromName<Configuration.Notifications>(q).Equals(p.Key)))
                                        {
                                            case true: // If the type of the notification is not of the selected type, then add the notification.
                                                lock (NotificationDestinationLock)
                                                {
                                                    notificationDestination.Add(p.Key, p.Value);
                                                }
                                                break;

                                            default:
                                                // Filter the notification destination by the supplied URL.
                                                var URLs = new HashSet<string>(p.Value.Keys
                                                    .AsParallel().Where(q => !q.Contains(url)).Select(q => q));
                                                // If no URL matches then the notification should not be added because it does not exist.
                                                if (!URLs.Any())
                                                    return;
                                                lock (NotificationDestinationLock)
                                                {
                                                    var URLTags = new HashSet<string>(p.Value.Values.SelectMany(q => q));
                                                    notificationDestination.Add(p.Key,
                                                        new SerializableDictionary<string, HashSet<string>>());
                                                    foreach (var URL in URLs)
                                                    {
                                                        notificationDestination[p.Key].Add(URL, URLTags);
                                                    }
                                                }
                                                break;
                                        }
                                    });
                                    lock (LockObject)
                                    {
                                        groupNotifications.Add(new Notifications
                                        {
                                            GroupName = o.GroupName,
                                            GroupUUID = o.GroupUUID,
                                            HTTPNotifications = notificationDestination,
                                            Data = o.Data,
                                            Afterburn = o.Afterburn
                                        });
                                    }
                                });
                                // Now assign the new notifications.
                                lock (GroupNotificationsLock)
                                {
                                    GroupNotifications = groupNotifications;
                                }
                            }
                            // Save the notifications state.
                            SaveNotificationState.Invoke();
                            break;

                        case Enumerations.Action.LIST:
                            var csv = new List<string>();
                            var notifications = new HashSet<Configuration.Notifications>(
                                CSV.ToEnumerable(notificationTypes)
                                .AsParallel()
                                .Where(o => !string.IsNullOrEmpty(o))
                                .Distinct()
                                .Select(o => Reflection.GetEnumValueFromName<Configuration.Notifications>(o)));
                            lock (GroupNotificationsLock)
                            {
                                var groupNotification =
                                    GroupNotifications.AsParallel().FirstOrDefault(
                                        o => o.GroupUUID.Equals(corradeCommandParameters.Group.UUID));
                                if (groupNotification != null)
                                {
                                    Reflection.GetEnumNames<Configuration.Notifications>()
                                        .AsParallel()
                                        .Select(o => new { Type = Reflection.GetEnumValueFromName<Configuration.Notifications>(o) })
                                        .Where(o => groupNotification.NotificationMask.IsMaskFlagSet(o.Type))
                                        .Select(o => new
                                        {
                                            Type = o.Type,
                                            URLs = groupNotification.HTTPNotifications[o.Type].Keys,
                                            Tags = groupNotification.HTTPNotifications[o.Type].Values.SelectMany(p => p),
                                        })
                                        // http://grimore.org/fuss/lambda_calculus/functional_programming/aggreagators/switch
                                        .Switch(
                                            o =>
                                            {
                                                // No filtering select so just jump everything.
                                                lock (LockObject)
                                                {
                                                    csv.Add(Reflection.GetNameFromEnumValue(o.Type));
                                                    csv.AddRange(o.URLs);
                                                    csv.AddRange(o.Tags);
                                                }
                                            }, o => notifications.Any() && notifications.Contains(o.Type), o =>
                                            {
                                                // Filter by type.
                                                lock (LockObject)
                                                {
                                                    csv.Add(Reflection.GetNameFromEnumValue(o.Type));
                                                    csv.AddRange(o.URLs);
                                                }
                                                return true;
                                            },
                                            o => tags.Any() && o.Tags.Intersect(tags).Any(), o =>
                                            {
                                                // Filter by tag.
                                                lock (LockObject)
                                                {
                                                    csv.AddRange(o.Tags.Intersect(tags));
                                                    csv.AddRange(o.URLs);
                                                }
                                                return true;
                                            });
                                }
                            }
                            if (csv.Any())
                            {
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                    CSV.FromEnumerable(csv));
                            }
                            break;

                        case Enumerations.Action.CLEAR:
                            lock (GroupNotificationsLock)
                            {
                                GroupNotifications.AsParallel().ForAll(o =>
                                {
                                    switch (!o.GroupUUID.Equals(corradeCommandParameters.Group.UUID))
                                    {
                                        case false: // this is our group
                                            var notificationDestination = new SerializableDictionary<Configuration.Notifications,
                                                SerializableDictionary<string, HashSet<string>>>();
                                            var NotificationDestinationLock = new object();
                                            o.HTTPNotifications.AsParallel().ForAll(p =>
                                            {
                                                switch (!CSV.ToEnumerable(notificationTypes)
                                                    .AsParallel()
                                                    .Where(q => !string.IsNullOrEmpty(q))
                                                    .Any(
                                                        q =>
                                                            Reflection
                                                                .GetEnumValueFromName<Configuration.Notifications>(q)
                                                                .Equals(p.Key)))
                                                {
                                                    case true:
                                                        lock (NotificationDestinationLock)
                                                        {
                                                            notificationDestination.Add(p.Key, p.Value);
                                                        }
                                                        break;
                                                }
                                            });
                                            lock (LockObject)
                                            {
                                                groupNotifications.Add(new Notifications
                                                {
                                                    GroupName = o.GroupName,
                                                    GroupUUID = o.GroupUUID,
                                                    HTTPNotifications = notificationDestination,
                                                    Data = o.Data,
                                                    Afterburn = o.Afterburn
                                                });
                                            }
                                            break;

                                        default: // not our group
                                            lock (LockObject)
                                            {
                                                groupNotifications.Add(o);
                                            }
                                            break;
                                    }
                                });
                                GroupNotifications = groupNotifications;
                            }
                            // Save the notifications state.
                            SaveNotificationState.Invoke();
                            break;

                        case Enumerations.Action.PURGE:
                            lock (GroupNotificationsLock)
                            {
                                GroupNotifications.RemoveWhere(
                                    o => o.GroupUUID.Equals(corradeCommandParameters.Group.UUID));
                            }
                            // Save the notifications state.
                            SaveNotificationState.Invoke();
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }

                    // If notifications changed, rebuild the notification cache.
                    switch (action)
                    {
                        case Enumerations.Action.ADD:
                        case Enumerations.Action.SET:
                        case Enumerations.Action.REMOVE:
                        case Enumerations.Action.CLEAR:
                        case Enumerations.Action.PURGE:
                            // Build the group notification cache.
                            lock (GroupNotificationsLock)
                            {
                                GroupNotificationsCache.Clear();
                                new List<Configuration.Notifications>(
                                    Reflection.GetEnumValues<Configuration.Notifications>())
                                    .AsParallel().ForAll(o =>
                                    {
                                        GroupNotifications.AsParallel()
                                            .Where(p => p.NotificationMask.IsMaskFlagSet(o))
                                            .ForAll(p =>
                                            {
                                                lock (LockObject)
                                                {
                                                    if (GroupNotificationsCache.ContainsKey(o))
                                                    {
                                                        GroupNotificationsCache[o].Add(p);
                                                        return;
                                                    }
                                                    GroupNotificationsCache.Add(o, new HashSet<Notifications> { p });
                                                }
                                            });
                                    });
                            }
                            break;
                    }
                };
        }
    }
}
