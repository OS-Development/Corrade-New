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
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> collision =
                (corradeNotificationParameters, notificationData) =>
                {
                    MeanCollisionEventArgs meanCollisionEventArgs =
                        (MeanCollisionEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(meanCollisionEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.AGGRESSOR),
                        meanCollisionEventArgs.Aggressor.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.MAGNITUDE),
                        meanCollisionEventArgs.Magnitude.ToString(Utils.EnUsCulture));
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.TIME),
                        meanCollisionEventArgs.Time.ToLongDateString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY),
                        meanCollisionEventArgs.Type.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.VICTIM),
                        meanCollisionEventArgs.Victim.ToString());
                };
        }
    }
}