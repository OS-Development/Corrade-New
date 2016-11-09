///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Helpers;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<NotificationParameters, Dictionary<string, string>> store =
                (corradeNotificationParameters, notificationData) =>
                {
                    var inventoryEventType = corradeNotificationParameters.Event.GetType();
                    if (inventoryEventType == typeof(InventoryObjectAddedEventArgs))
                    {
                        var inventoryObjectAddedEventArgs =
                            (InventoryObjectAddedEventArgs)corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(
                                    inventoryObjectAddedEventArgs.GetStructuredData(
                                        CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var LockObject = new object();
                        Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .Where(o => o.Key.Equals(typeof(InventoryObjectAddedEventArgs).FullName))
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    new List<object> { inventoryObjectAddedEventArgs },
                                    notificationData, LockObject, languageDetector,
                                    GroupBayesClassifiers[corradeNotificationParameters.Notification.GroupUUID]);
                            }));
                        return;
                    }

                    if (inventoryEventType == typeof(InventoryObjectRemovedEventArgs))
                    {
                        var inventoryObjectRemovedEventArgs =
                            (InventoryObjectRemovedEventArgs)corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(
                                    inventoryObjectRemovedEventArgs.GetStructuredData(
                                        CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var LockObject = new object();
                        Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .Where(o => o.Key.Equals(typeof(InventoryObjectRemovedEventArgs).FullName))
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    new List<object> { inventoryObjectRemovedEventArgs },
                                    notificationData, LockObject, languageDetector,
                                    GroupBayesClassifiers[corradeNotificationParameters.Notification.GroupUUID]);
                            }));
                        return;
                    }

                    if (inventoryEventType == typeof(InventoryObjectUpdatedEventArgs))
                    {
                        var inventoryObjectUpdatedEventArgs =
                            (InventoryObjectUpdatedEventArgs)corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(
                                    inventoryObjectUpdatedEventArgs.GetStructuredData(
                                        CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var LockObject = new object();
                        Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .Where(o => o.Key.Equals(typeof(InventoryObjectUpdatedEventArgs).FullName))
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    new List<object> { inventoryObjectUpdatedEventArgs },
                                    notificationData, LockObject, languageDetector,
                                    GroupBayesClassifiers[corradeNotificationParameters.Notification.GroupUUID]);
                            }));
                    }
                };
        }
    }
}