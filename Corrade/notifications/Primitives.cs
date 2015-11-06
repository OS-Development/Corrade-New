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
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> primitives =
                (corradeNotificationParameters, notificationData) =>
                {
                    System.Type radarPrimitivesType = corradeNotificationParameters.Event.GetType();
                    if (radarPrimitivesType == typeof (PrimEventArgs))
                    {
                        PrimEventArgs primEventArgs =
                            (PrimEventArgs) corradeNotificationParameters.Event;
                        lock (RadarObjectsLock)
                        {
                            if (RadarObjects.ContainsKey(primEventArgs.Prim.ID)) return;
                            RadarObjects.Add(primEventArgs.Prim.ID, primEventArgs.Prim);
                        }
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                                CSV.FromEnumerable(GetStructuredData(primEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.OWNER),
                            primEventArgs.Prim.OwnerID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ID),
                            primEventArgs.Prim.ID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION),
                            primEventArgs.Prim.Position.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ROTATION),
                            primEventArgs.Prim.Rotation.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY),
                            primEventArgs.Prim.PrimData.PCode.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                            Reflection.GetNameFromEnumValue(Action.APPEAR));
                        return;
                    }
                    if (radarPrimitivesType == typeof (KillObjectEventArgs))
                    {
                        KillObjectEventArgs killObjectEventArgs =
                            (KillObjectEventArgs) corradeNotificationParameters.Event;
                        Primitive prim;
                        lock (RadarObjectsLock)
                        {
                            KeyValuePair<UUID, Primitive> tracked =
                                RadarObjects.AsParallel().FirstOrDefault(
                                    p => p.Value.LocalID.Equals(killObjectEventArgs.ObjectLocalID));
                            switch (!tracked.Equals(default(KeyValuePair<UUID, Primitive>)))
                            {
                                case true:
                                    RadarObjects.Remove(tracked.Key);
                                    prim = tracked.Value;
                                    break;
                                default:
                                    return;
                            }
                        }
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                                CSV.FromEnumerable(GetStructuredData(killObjectEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.OWNER),
                            prim.OwnerID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ID),
                            prim.ID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION),
                            prim.Position.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ROTATION),
                            prim.Rotation.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY),
                            prim.PrimData.PCode.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                            Reflection.GetNameFromEnumValue(Action.VANISH));
                    }
                };
        }
    }
}