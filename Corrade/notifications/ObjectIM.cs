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
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> objectim =
                (corradeNotificationParameters, notificationData) =>
                {
                    InstantMessageEventArgs notificationObjectInstantMessage =
                        (InstantMessageEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(notificationObjectInstantMessage,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.OWNER),
                        notificationObjectInstantMessage.IM.FromAgentID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM),
                        notificationObjectInstantMessage.IM.IMSessionID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.NAME),
                        notificationObjectInstantMessage.IM.FromAgentName);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE),
                        notificationObjectInstantMessage.IM.Message);
                };
        }
    }
}