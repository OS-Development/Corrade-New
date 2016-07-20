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
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> rlv =
                (corradeNotificationParameters, notificationData) =>
                {
                    var RLVEventArgs = (ChatEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(RLVEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM),
                        RLVEventArgs.SourceID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.NAME),
                        RLVEventArgs.FromName);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION),
                        RLVEventArgs.Position.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.RLV),
                        CSV.FromEnumerable(wasRLVToString(RLVEventArgs.Message)));
                };
        }
    }
}