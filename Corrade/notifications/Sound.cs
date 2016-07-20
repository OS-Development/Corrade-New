///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> sound =
                (corradeNotificationParameters, notificationData) =>
                {
                    var alertMessageEventArgs =
                        (SoundTriggerEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(alertMessageEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.GAIN),
                        alertMessageEventArgs.Gain.ToString(CultureInfo.InvariantCulture));
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM),
                        alertMessageEventArgs.ObjectID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.OWNER),
                        alertMessageEventArgs.OwnerID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.PARENT),
                        alertMessageEventArgs.ParentID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION),
                        alertMessageEventArgs.Position.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.REGION),
                        alertMessageEventArgs.Simulator.Name);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ASSET),
                        alertMessageEventArgs.SoundID.ToString());
                };
        }
    }
}