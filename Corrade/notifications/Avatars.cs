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
            public static Action<NotificationParameters, Dictionary<string, string>> avatars =
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
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(wasOpenMetaverse.Reflection.GetStructuredData(avatarUpdateEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var LockObject = new object();
                        Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    new List<object> {avatarUpdateEventArgs},
                                    notificationData, LockObject, languageDetector,
                                    GroupBayesClassifiers[corradeNotificationParameters.Notification.GroupUUID]);
                            }));
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
                                    new List<object> {avatar},
                                    notificationData, LockObject, languageDetector,
                                    GroupBayesClassifiers[corradeNotificationParameters.Notification.GroupUUID]);
                            }));
                    }
                };
        }
    }
}