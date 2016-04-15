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
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> sit =
                (corradeNotificationParameters, notificationData) =>
                {
                    AvatarSitChangedEventArgs sitChangedEventArgs =
                        (AvatarSitChangedEventArgs)corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(sitChangedEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.REGION),
                        sitChangedEventArgs.Simulator.Name);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME),
                        sitChangedEventArgs.Avatar.FirstName);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME),
                        sitChangedEventArgs.Avatar.LastName);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT),
                        sitChangedEventArgs.Avatar.ID.ToString());
                    KeyValuePair<uint, Primitive> oldPrimitive = sitChangedEventArgs.Simulator.ObjectsPrimitives.Copy()
                        .AsParallel()
                        .FirstOrDefault(o => o.Key.Equals(sitChangedEventArgs.OldSeat));
                    if (!oldPrimitive.Equals(default(KeyValuePair<uint, Primitive>)))
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.OLD),
                            oldPrimitive.Value.ID.ToString());
                    }
                    KeyValuePair<uint, Primitive> newPrimitive = sitChangedEventArgs.Simulator.ObjectsPrimitives.Copy()
                        .AsParallel()
                        .FirstOrDefault(o => o.Key.Equals(sitChangedEventArgs.SittingOn));
                    if (!newPrimitive.Equals(default(KeyValuePair<uint, Primitive>)))
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.NEW),
                            newPrimitive.Value.ID.ToString());
                    }
                    /*notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.OLD),
                        sitChangedEventArgs.Simulator.ObjectsPrimitives.Copy()
                            .AsParallel()
                            .FirstOrDefault(o => o.Key.Equals(sitChangedEventArgs.OldSeat))
                            .Value.ID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.NEW),
                        sitChangedEventArgs.Simulator.ObjectsPrimitives.Copy()
                            .AsParallel()
                            .FirstOrDefault(o => o.Key.Equals(sitChangedEventArgs.SittingOn))
                            .Value.ID.ToString());*/
                };
        }
    }
}