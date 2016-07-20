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
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> primitives =
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
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                                CSV.FromEnumerable(GetStructuredData(killObjectEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.OWNER),
                            primitive.OwnerID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ID),
                            primitive.ID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION),
                            primitive.Position.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ROTATION),
                            primitive.Rotation.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY),
                            primitive.PrimData.PCode.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                            Reflection.GetNameFromEnumValue(Action.VANISH));
                    }
                };
        }
    }
}