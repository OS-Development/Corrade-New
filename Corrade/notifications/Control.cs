///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeNotifications
        {
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> control =
                (corradeNotificationParameters, notificationData) =>
                {
                    ScriptControlEventArgs scriptControlEventArgs =
                        (ScriptControlEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(scriptControlEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.CONTROLS),
                        CSV.FromEnumerable(typeof (ScriptControlChange).GetFields(BindingFlags.Public |
                                                                                  BindingFlags.Static)
                            .AsParallel().Where(
                                p =>
                                    !(((uint) p.GetValue(null) &
                                       (uint) scriptControlEventArgs.Controls)).Equals(0))
                            .Select(p => p.Name)));
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.PASS),
                        scriptControlEventArgs.Pass.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.TAKE),
                        scriptControlEventArgs.Take.ToString());
                };
        }
    }
}