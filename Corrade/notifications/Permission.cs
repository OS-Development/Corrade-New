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
        public static partial class CorradeNotifications
        {
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> permission =
                (corradeNotificationParameters, notificationData) =>
                {
                    ScriptQuestionEventArgs scriptQuestionEventArgs =
                        (ScriptQuestionEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(scriptQuestionEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM),
                        scriptQuestionEventArgs.ItemID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.TASK),
                        scriptQuestionEventArgs.TaskID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.PERMISSIONS),
                        CSV.FromEnumerable(typeof (ScriptPermission).GetFields(BindingFlags.Public |
                                                                               BindingFlags.Static)
                            .AsParallel().Where(
                                p =>
                                    !(((int) p.GetValue(null) &
                                       (int) scriptQuestionEventArgs.Questions)).Equals(0))
                            .Select(p => p.Name)));
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.REGION),
                        scriptQuestionEventArgs.Simulator.Name);
                };
        }
    }
}