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
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> avatars =
                (corradeNotificationParameters, notificationData) =>
                {
                    var radarAvatarsType = corradeNotificationParameters.Event.GetType();
                    if (radarAvatarsType == typeof (AvatarUpdateEventArgs))
                    {
                        var avatarUpdateEventArgs =
                            (AvatarUpdateEventArgs) corradeNotificationParameters.Event;
                        lock (RadarObjectsLock)
                        {
                            if (RadarObjects.ContainsKey(avatarUpdateEventArgs.Avatar.LocalID)) return;
                            RadarObjects.Add(avatarUpdateEventArgs.Avatar.LocalID, avatarUpdateEventArgs.Avatar);
                        }
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                                CSV.FromEnumerable(GetStructuredData(avatarUpdateEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME),
                            avatarUpdateEventArgs.Avatar.FirstName);
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME),
                            avatarUpdateEventArgs.Avatar.LastName);
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ID),
                            avatarUpdateEventArgs.Avatar.ID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION),
                            avatarUpdateEventArgs.Avatar.Position.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ROTATION),
                            avatarUpdateEventArgs.Avatar.Rotation.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY),
                            avatarUpdateEventArgs.Avatar.PrimData.PCode.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                            Reflection.GetNameFromEnumValue(Action.APPEAR));
                        return;
                    }
                    if (radarAvatarsType == typeof (KillObjectEventArgs))
                    {
                        var killObjectEventArgs =
                            (KillObjectEventArgs) corradeNotificationParameters.Event;
                        Avatar avatar;
                        lock (RadarObjectsLock)
                        {
                            Primitive primitive;
                            switch (RadarObjects.TryGetValue(killObjectEventArgs.ObjectLocalID, out primitive))
                            {
                                case true:
                                    RadarObjects.Remove(killObjectEventArgs.ObjectLocalID);
                                    break;
                                default:
                                    return;
                            }
                            if (!(primitive is Avatar)) return;
                            avatar = primitive as Avatar;
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
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME),
                            avatar.FirstName);
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME),
                            avatar.LastName);
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ID),
                            avatar.ID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION),
                            avatar.Position.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ROTATION),
                            avatar.Rotation.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY),
                            avatar.PrimData.PCode.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                            Reflection.GetNameFromEnumValue(Action.VANISH));
                    }
                };
        }
    }
}