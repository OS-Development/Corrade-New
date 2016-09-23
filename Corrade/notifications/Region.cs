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
            public static Action<NotificationParameters, Dictionary<string, string>> region =
                (corradeNotificationParameters, notificationData) =>
                {
                    var notificationRegionMessage =
                        (InstantMessageEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                            CSV.FromEnumerable(wasOpenMetaverse.Reflection.GetStructuredData(notificationRegionMessage,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }

                    var LockObject = new object();
                    Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                        .NotificationParameters.AsParallel()
                        .ForAll(o => o.Value.AsParallel().ForAll(p =>
                        {
                            p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                new List<object> {notificationRegionMessage},
                                notificationData, LockObject, rankedLanguageIdentifier,
                                GroupBayesClassifiers[corradeNotificationParameters.Notification.GroupUUID]);
                        }));
                };
        }
    }
}