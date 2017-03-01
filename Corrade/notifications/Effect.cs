///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using Corrade.Helpers;
using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Linq;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<NotificationParameters, Dictionary<string, string>> effect =
                (corradeNotificationParameters, notificationData) =>
                {
                    var viewerEffectType = corradeNotificationParameters.Event.GetType();
                    if (viewerEffectType == typeof(ViewerEffectEventArgs))
                    {
                        var notificationViewerEffectEventArgs =
                            (ViewerEffectEventArgs)corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification != null && corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(
                                    wasOpenMetaverse.Reflection.GetStructuredData(notificationViewerEffectEventArgs,
                                        CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var LockObject = new object();
                        Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    new List<object> { notificationViewerEffectEventArgs },
                                    notificationData, LockObject, languageDetector,
                                    GroupBayesClassifiers[corradeNotificationParameters.Notification.GroupUUID]);
                            }));
                        return;
                    }
                    if (viewerEffectType == typeof(ViewerEffectPointAtEventArgs))
                    {
                        var notificationViewerPointAtEventArgs =
                            (ViewerEffectPointAtEventArgs)corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification != null && corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(
                                    wasOpenMetaverse.Reflection.GetStructuredData(notificationViewerPointAtEventArgs,
                                        CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var LockObject = new object();
                        Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    new List<object> { notificationViewerPointAtEventArgs },
                                    notificationData, LockObject, languageDetector,
                                    GroupBayesClassifiers[corradeNotificationParameters.Notification.GroupUUID]);
                            }));
                        return;
                    }
                    if (viewerEffectType == typeof(ViewerEffectLookAtEventArgs))
                    {
                        var notificationViewerLookAtEventArgs =
                            (ViewerEffectLookAtEventArgs)corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification != null && corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(
                                    wasOpenMetaverse.Reflection.GetStructuredData(notificationViewerLookAtEventArgs,
                                        CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var LockObject = new object();
                        Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    new List<object> { notificationViewerLookAtEventArgs },
                                    notificationData, LockObject, languageDetector,
                                    GroupBayesClassifiers[corradeNotificationParameters.Notification.GroupUUID]);
                            }));
                    }
                };
        }
    }
}
