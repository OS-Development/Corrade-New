///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> feed =
                (corradeNotificationParameters, notificationData) =>
                {
                    FeedEventArgs feedEventArgs =
                        (FeedEventArgs) corradeNotificationParameters.Event;
                    // Set-up filters.
                    if (!feedEventArgs.GroupUUID.Equals(corradeNotificationParameters.Notification.GroupUUID))
                        return;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(feedEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.TITLE),
                        feedEventArgs.Title);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.SUMMARY),
                        feedEventArgs.Summary);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.NAME),
                        feedEventArgs.Name);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATE),
                        feedEventArgs.Date.DateTime.ToString(Constants.LSL.DATE_TIME_STAMP));
                };
        }
    }
}