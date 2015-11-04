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
using CorradeConfiguration;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> notify =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Notifications))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string url = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.URL)),
                            corradeCommandParameters.Message));
                    string notificationTypes =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant();
                    Action action =
                        Reflection.GetEnumValueFromName<Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant());
                    object LockObject = new object();
                    HashSet<Notification> groupNotifications = new HashSet<Notification>();
                    switch (action)
                    {
                        case Action.SET:
                        case Action.ADD:
                            if (string.IsNullOrEmpty(url))
                            {
                                throw new ScriptException(ScriptError.INVALID_URL_PROVIDED);
                            }
                            Uri notifyURL;
                            if (!Uri.TryCreate(url, UriKind.Absolute, out notifyURL))
                            {
                                throw new ScriptException(ScriptError.INVALID_URL_PROVIDED);
                            }
                            if (string.IsNullOrEmpty(notificationTypes))
                            {
                                throw new ScriptException(ScriptError.INVALID_NOTIFICATION_TYPES);
                            }
                            Notification notification;
                            lock (GroupNotificationsLock)
                            {
                                notification =
                                    GroupNotifications.AsParallel().FirstOrDefault(
                                        o =>
                                            o.GroupName.Equals(corradeCommandParameters.Group.Name,
                                                StringComparison.OrdinalIgnoreCase));
                            }
                            // Get any afterburn data.
                            string afterBurnData =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.AFTERBURN)),
                                        corradeCommandParameters.Message));
                            Collections.SerializableDictionary<string, string> afterburn =
                                new Collections.SerializableDictionary<string, string>();
                            if (!string.IsNullOrEmpty(afterBurnData))
                            {
                                HashSet<string> results = new HashSet<string>(Reflection.GetEnumNames<ResultKeys>());
                                HashSet<string> scripts = new HashSet<string>(Reflection.GetEnumNames<ScriptKeys>());
                                Parallel.ForEach(CSV.ToKeyValue(afterBurnData), o =>
                                {
                                    // remove keys that are script keys, result keys or invalid key-value pairs
                                    if (string.IsNullOrEmpty(o.Key) || results.Contains(wasInput(o.Key)) ||
                                        scripts.Contains(wasInput(o.Key)) ||
                                        string.IsNullOrEmpty(o.Value))
                                        return;
                                    lock (LockObject)
                                    {
                                        afterburn.Add(o.Key, o.Value);
                                    }
                                });
                            }
                            // Build any requested data for raw notifications.
                            string fields =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                        corradeCommandParameters.Message));
                            HashSet<string> data = new HashSet<string>();
                            if (!string.IsNullOrEmpty(fields))
                            {
                                Parallel.ForEach(
                                    CSV.ToEnumerable(fields).AsParallel().Where(o => !string.IsNullOrEmpty(o)),
                                    o =>
                                    {
                                        lock (LockObject)
                                        {
                                            data.Add(o);
                                        }
                                    });
                            }
                            switch (!notification.Equals(default(Notification)))
                            {
                                case false:
                                    notification = new Notification
                                    {
                                        GroupName = corradeCommandParameters.Group.Name,
                                        GroupUUID = corradeCommandParameters.Group.UUID,
                                        NotificationURLDestination =
                                            new Collections.SerializableDictionary
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
                                            new Collections.SerializableDictionary
                                                <Configuration.Notifications, HashSet<string>>();
                                    }
                                    break;
                            }
                            bool succeeded = true;
                            Parallel.ForEach(CSV.ToEnumerable(
                                notificationTypes).AsParallel().Where(o => !string.IsNullOrEmpty(o)),
                                (o, state) =>
                                {
                                    uint notificationValue =
                                        (uint) Reflection.GetEnumValueFromName<Configuration.Notifications>(o);
                                    if (!GroupHasNotification(corradeCommandParameters.Group.Name, notificationValue))
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
                                            // notification destination is already there
                                            if (notification.NotificationURLDestination[
                                                (Configuration.Notifications) notificationValue].Contains(url))
                                                break;
                                            switch (action)
                                            {
                                                case Action.ADD:
                                                    lock (LockObject)
                                                    {
                                                        notification.NotificationURLDestination[
                                                            (Configuration.Notifications) notificationValue]
                                                            .Add(url);
                                                    }
                                                    break;
                                                case Action.SET:
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
                                    throw new ScriptException(ScriptError.NOTIFICATION_NOT_ALLOWED);
                            }
                            lock (GroupNotificationsLock)
                            {
                                // Replace notification.
                                GroupNotifications.RemoveWhere(
                                    o =>
                                        o.GroupName.Equals(corradeCommandParameters.Group.Name,
                                            StringComparison.OrdinalIgnoreCase));
                                GroupNotifications.Add(notification);
                            }
                            // Save the notifications state.
                            SaveNotificationState.Invoke();
                            break;
                        case Action.REMOVE:
                            lock (GroupNotificationsLock)
                            {
                                Parallel.ForEach(GroupNotifications, o =>
                                {
                                    if ((!CSV.ToEnumerable(notificationTypes)
                                        .AsParallel()
                                        .Where(p => !string.IsNullOrEmpty(p))
                                        .Any(p => !(o.NotificationMask &
                                                    (uint)
                                                        Reflection.GetEnumValueFromName<Configuration.Notifications>(
                                                            p))
                                            .Equals(0)) &&
                                         !o.NotificationURLDestination.Values.Any(p => p.Contains(url))) ||
                                        !o.GroupName.Equals(corradeCommandParameters.Group.Name,
                                            StringComparison.OrdinalIgnoreCase))
                                    {
                                        lock (LockObject)
                                        {
                                            groupNotifications.Add(o);
                                        }
                                        return;
                                    }
                                    Collections.SerializableDictionary<Configuration.Notifications, HashSet<string>>
                                        notificationDestination =
                                            new Collections.SerializableDictionary
                                                <Configuration.Notifications, HashSet<string>>();
                                    object NotficatinDestinationLock = new object();
                                    Parallel.ForEach(o.NotificationURLDestination, p =>
                                    {
                                        switch (!CSV.ToEnumerable(notificationTypes)
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
                                                HashSet<string> URLs =
                                                    new HashSet<string>(
                                                        p.Value.AsParallel()
                                                            .Where(q => !q.Equals(url, StringComparison.Ordinal)));
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
                        case Action.LIST:
                            // If the group has no installed notifications, bail
                            List<string> csv = new List<string>();
                            lock (GroupNotificationsLock)
                            {
                                Notification groupNotification =
                                    GroupNotifications.AsParallel().FirstOrDefault(
                                        o =>
                                            o.GroupName.Equals(corradeCommandParameters.Group.Name,
                                                StringComparison.OrdinalIgnoreCase));
                                if (!groupNotification.Equals(default(Notification)))
                                {
                                    Parallel.ForEach(Reflection.GetEnumNames<Configuration.Notifications>(), o =>
                                    {
                                        if ((groupNotification.NotificationMask &
                                             (uint) Reflection.GetEnumValueFromName<Configuration.Notifications>(o))
                                            .Equals(0))
                                            return;
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
                                result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                                    CSV.FromEnumerable(csv));
                            }
                            break;
                        case Action.CLEAR:
                            lock (GroupNotificationsLock)
                            {
                                Parallel.ForEach(GroupNotifications, o =>
                                {
                                    switch (!o.GroupName.Equals(corradeCommandParameters.Group.Name))
                                    {
                                        case false: // this is our group
                                            Collections.SerializableDictionary
                                                <Configuration.Notifications, HashSet<string>>
                                                notificationDestination =
                                                    new Collections.SerializableDictionary
                                                        <Configuration.Notifications, HashSet<string>>();
                                            Parallel.ForEach(o.NotificationURLDestination, p =>
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
                        case Action.PURGE:
                            lock (GroupNotificationsLock)
                            {
                                Notification groupNotification =
                                    GroupNotifications.AsParallel().FirstOrDefault(
                                        o =>
                                            o.GroupName.Equals(corradeCommandParameters.Group.Name,
                                                StringComparison.OrdinalIgnoreCase));
                                if (!groupNotification.Equals(default(Notification)))
                                {
                                    GroupNotifications.Remove(groupNotification);
                                }
                            }
                            // Save the notifications state.
                            SaveNotificationState.Invoke();
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}