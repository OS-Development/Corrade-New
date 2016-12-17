///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using CorradeConfigurationSharp;
using wasSharp;
using wasSharp.Collections.Generic;

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
                            (int) Configuration.Permissions.Notifications))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var url = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.URL)),
                            corradeCommandParameters.Message));
                    var notificationTypes =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant();
                    var action =
                        Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant());
                    var LockObject = new object();
                    var groupNotifications = new HashSet<Notification>();
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
                            Notification notification;
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
                                    .ToArray()
                                    .AsParallel()
                                    .Where(
                                        o =>
                                            !string.IsNullOrEmpty(o.Key) && !results.Contains(wasInput(o.Key)) &&
                                            !string.IsNullOrEmpty(o.Value) && !scripts.Contains(wasInput(o.Key)))
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
                                    .ToArray()
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
                                    notification = new Notification
                                    {
                                        GroupName = corradeCommandParameters.Group.Name,
                                        GroupUUID = corradeCommandParameters.Group.UUID,
                                        NotificationURLDestination =
                                            new SerializableDictionary
                                                <Configuration.Notifications, HashSet<string>>(),
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
                                    if (notification.NotificationURLDestination == null)
                                    {
                                        notification.NotificationURLDestination =
                                            new SerializableDictionary
                                                <Configuration.Notifications, HashSet<string>>();
                                    }
                                    break;
                            }
                            var succeeded = true;
                            Parallel.ForEach(CSV.ToEnumerable(
                                notificationTypes).AsParallel().Where(o => !string.IsNullOrEmpty(o)),
                                (o, state) =>
                                {
                                    var notificationValue =
                                        (ulong) Reflection.GetEnumValueFromName<Configuration.Notifications>(o);
                                    if (!GroupHasNotification(corradeCommandParameters.Group.UUID, notificationValue))
                                    {
                                        // one of the notification was not allowed, so abort
                                        succeeded = false;
                                        state.Break();
                                    }
                                    notification.Data = data;
                                    notification.Afterburn = afterburn;
                                    switch (
                                        !notification.NotificationURLDestination.ContainsKey(
                                            (Configuration.Notifications) notificationValue))
                                    {
                                        case true:
                                            lock (LockObject)
                                            {
                                                notification.NotificationURLDestination.Add(
                                                    (Configuration.Notifications) notificationValue,
                                                    new HashSet<string> {url});
                                            }
                                            break;
                                        default:
                                            switch (action)
                                            {
                                                case Enumerations.Action.ADD:
                                                    // notification destination is already there
                                                    if (notification.NotificationURLDestination[
                                                        (Configuration.Notifications) notificationValue].Contains(url))
                                                        break;
                                                    lock (LockObject)
                                                    {
                                                        notification.NotificationURLDestination[
                                                            (Configuration.Notifications) notificationValue]
                                                            .Add(url);
                                                    }
                                                    break;
                                                case Enumerations.Action.SET:
                                                    lock (LockObject)
                                                    {
                                                        notification.NotificationURLDestination[
                                                            (Configuration.Notifications) notificationValue] = new HashSet
                                                                <string>
                                                            {
                                                                url
                                                            };
                                                    }
                                                    break;
                                            }

                                            break;
                                    }
                                });
                            switch (succeeded)
                            {
                                case false:
                                    throw new Command.ScriptException(Enumerations.ScriptError.NOTIFICATION_NOT_ALLOWED);
                            }
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
                                    if ((!CSV.ToEnumerable(notificationTypes)
                                        .ToArray()
                                        .AsParallel()
                                        .Where(p => !string.IsNullOrEmpty(p))
                                        .Any(
                                            p =>
                                                o.NotificationMask.IsMaskFlagSet(Reflection
                                                    .GetEnumValueFromName<Configuration.Notifications>(
                                                        p))) &&
                                         !o.NotificationURLDestination.Values.Any(p => p.Contains(url))) ||
                                        !o.GroupUUID.Equals(corradeCommandParameters.Group.UUID))
                                    {
                                        lock (LockObject)
                                        {
                                            groupNotifications.Add(o);
                                        }
                                        return;
                                    }
                                    var
                                        notificationDestination =
                                            new SerializableDictionary
                                                <Configuration.Notifications, HashSet<string>>();
                                    var NotficatinDestinationLock = new object();
                                    o.NotificationURLDestination.AsParallel().ForAll(p =>
                                    {
                                        switch (!CSV.ToEnumerable(notificationTypes)
                                            .ToArray()
                                            .AsParallel()
                                            .Where(q => !string.IsNullOrEmpty(q))
                                            .Any(
                                                q =>
                                                    Reflection.GetEnumValueFromName<Configuration.Notifications>(q)
                                                        .Equals(p.Key)))
                                        {
                                            case true:
                                                lock (NotficatinDestinationLock)
                                                {
                                                    notificationDestination.Add(p.Key, p.Value);
                                                }
                                                break;
                                            default:
                                                var URLs =
                                                    new HashSet<string>(
                                                        p.Value.AsParallel()
                                                            .Where(
                                                                q =>
                                                                    !Strings.StringEquals(url, q,
                                                                        StringComparison.Ordinal)));
                                                if (!URLs.Any()) return;
                                                lock (NotficatinDestinationLock)
                                                {
                                                    notificationDestination.Add(p.Key, URLs);
                                                }
                                                break;
                                        }
                                    });
                                    lock (LockObject)
                                    {
                                        groupNotifications.Add(new Notification
                                        {
                                            GroupName = o.GroupName,
                                            GroupUUID = o.GroupUUID,
                                            NotificationURLDestination = notificationDestination,
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
                            // If the group has no installed notifications, bail
                            var csv = new List<string>();
                            lock (GroupNotificationsLock)
                            {
                                var groupNotification =
                                    GroupNotifications.AsParallel().FirstOrDefault(
                                        o =>
                                            o.GroupUUID.Equals(corradeCommandParameters.Group.UUID));
                                if (groupNotification != null)
                                {
                                    Reflection.GetEnumNames<Configuration.Notifications>()
                                        .ToArray()
                                        .AsParallel()
                                        .Where(
                                            o =>
                                                groupNotification.NotificationMask.IsMaskFlagSet(
                                                    Reflection.GetEnumValueFromName<Configuration.Notifications>(o)))
                                        .ForAll(o =>
                                        {
                                            lock (LockObject)
                                            {
                                                csv.Add(o);
                                                csv.AddRange(groupNotification.NotificationURLDestination[
                                                    Reflection.GetEnumValueFromName<Configuration.Notifications>(o)]);
                                            }
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
                                            var
                                                notificationDestination =
                                                    new SerializableDictionary
                                                        <Configuration.Notifications, HashSet<string>>();
                                            o.NotificationURLDestination.AsParallel().ForAll(p =>
                                            {
                                                switch (!CSV.ToEnumerable(notificationTypes)
                                                    .ToArray()
                                                    .AsParallel()
                                                    .Where(q => !string.IsNullOrEmpty(q))
                                                    .Any(
                                                        q =>
                                                            Reflection
                                                                .GetEnumValueFromName<Configuration.Notifications>(q)
                                                                .Equals(p.Key)))
                                                {
                                                    case true:
                                                        notificationDestination.Add(p.Key, p.Value);
                                                        break;
                                                }
                                            });
                                            groupNotifications.Add(new Notification
                                            {
                                                GroupName = o.GroupName,
                                                GroupUUID = o.GroupUUID,
                                                NotificationURLDestination = notificationDestination,
                                                Data = o.Data,
                                                Afterburn = o.Afterburn
                                            });
                                            break;
                                        default: // not our group
                                            groupNotifications.Add(o);
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
                                            .Where(p => p.NotificationMask.IsMaskFlagSet(o)).ForAll(p =>
                                            {
                                                lock (LockObject)
                                                {
                                                    if (GroupNotificationsCache.ContainsKey(o))
                                                    {
                                                        GroupNotificationsCache[o].Add(p);
                                                        return;
                                                    }
                                                    GroupNotificationsCache.Add(o, new HashSet<Notification> {p});
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