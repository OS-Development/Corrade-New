///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> url =
                (corradeNotificationParameters, notificationData) =>
                {
                    var loadURLEventArgs = (LoadUrlEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(loadURLEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.NAME),
                        loadURLEventArgs.ObjectName);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM),
                        loadURLEventArgs.ObjectID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.OWNER),
                        loadURLEventArgs.OwnerID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.GROUP),
                        loadURLEventArgs.OwnerIsGroup.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE),
                        loadURLEventArgs.Message);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.URL),
                        loadURLEventArgs.URL);
                };
        }
    }
}