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
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<NotificationParameters, Dictionary<string, string>> friendship =
                (corradeNotificationParameters, notificationData) =>
                {
                    var friendshipNotificationType = corradeNotificationParameters.Event.GetType();
                    if (friendshipNotificationType == typeof (FriendInfoEventArgs))
                    {
                        var friendInfoEventArgs =
                            (FriendInfoEventArgs) corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(wasOpenMetaverse.Reflection.GetStructuredData(friendInfoEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var LockObject = new object();
                        Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    new List<object> {friendInfoEventArgs},
                                    notificationData, LockObject, rankedLanguageIdentifier);
                            }));
                        return;
                    }
                    if (friendshipNotificationType == typeof (FriendshipResponseEventArgs))
                    {
                        var friendshipResponseEventArgs =
                            (FriendshipResponseEventArgs) corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(
                                    wasOpenMetaverse.Reflection.GetStructuredData(friendshipResponseEventArgs,
                                        CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var LockObject = new object();
                        Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    new List<object> {friendshipResponseEventArgs},
                                    notificationData, LockObject, rankedLanguageIdentifier);
                            }));
                        return;
                    }
                    if (friendshipNotificationType == typeof (FriendshipOfferedEventArgs))
                    {
                        var friendshipOfferedEventArgs =
                            (FriendshipOfferedEventArgs) corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(
                                    wasOpenMetaverse.Reflection.GetStructuredData(friendshipOfferedEventArgs,
                                        CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var LockObject = new object();
                        Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    new List<object> {friendshipOfferedEventArgs},
                                    notificationData, LockObject, rankedLanguageIdentifier);
                            }));
                    }
                };
        }
    }
}