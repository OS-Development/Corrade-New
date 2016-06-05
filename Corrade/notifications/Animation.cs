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
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> animation =
                (corradeNotificationParameters, notificationData) =>
                {
                    AnimationsChangedEventArgs animationsChangedEventArgs =
                        (AnimationsChangedEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(animationsChangedEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    ;
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ANIMATION),
                        CSV.FromDictionary(animationsChangedEventArgs.Animations.Copy()
                            .ToDictionary(o => o.Key.ToString(), o => o.Value.ToString())));
                };
        }
    }
}