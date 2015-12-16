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
        public partial class CorradeNotifications
        {
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> typing =
                (corradeNotificationParameters, notificationData) =>
                {
                    TypingEventArgs notificationTypingMessageEventArgs =
                        (TypingEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(notificationTypingMessageEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT),
                        notificationTypingMessageEventArgs.AgentUUID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME),
                        notificationTypingMessageEventArgs.FirstName);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME),
                        notificationTypingMessageEventArgs.LastName);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                        Reflection.GetNameFromEnumValue(notificationTypingMessageEventArgs.Action));
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY),
                        Reflection.GetNameFromEnumValue(notificationTypingMessageEventArgs.Entity));
                };
        }
    }
}