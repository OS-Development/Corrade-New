///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Helpers;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<NotificationParameters, Dictionary<string, string>> primitives =
                (corradeNotificationParameters, notificationData) =>
                {
                    var radarPrimitivesType = corradeNotificationParameters.Event.GetType();
                    if (radarPrimitivesType == typeof (PrimEventArgs))
                    {
                        var primEventArgs =
                            (PrimEventArgs) corradeNotificationParameters.Event;
                        lock (RadarObjectsLock)
                        {
                            if (RadarObjects.ContainsKey(primEventArgs.Prim.LocalID)) return;
                            RadarObjects.Add(primEventArgs.Prim.LocalID, primEventArgs.Prim);
                        }
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(wasOpenMetaverse.Reflection.GetStructuredData(primEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var LockObject = new object();
                        Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    new List<object> {primEventArgs},
                                    notificationData, LockObject, rankedLanguageIdentifier);
                            }));
                        return;
                    }
                    if (radarPrimitivesType == typeof (KillObjectEventArgs))
                    {
                        var killObjectEventArgs =
                            (KillObjectEventArgs) corradeNotificationParameters.Event;
                        Primitive primitive;
                        lock (RadarObjectsLock)
                        {
                            switch (RadarObjects.TryGetValue(killObjectEventArgs.ObjectLocalID, out primitive))
                            {
                                case true:
                                    RadarObjects.Remove(killObjectEventArgs.ObjectLocalID);
                                    break;
                                default:
                                    return;
                            }
                        }
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(wasOpenMetaverse.Reflection.GetStructuredData(killObjectEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var LockObject = new object();
                        Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    new List<object> {primitive},
                                    notificationData, LockObject, rankedLanguageIdentifier);
                            }));
                    }
                };
        }
    }
}