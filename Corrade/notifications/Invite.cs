///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Helpers;
using Corrade.Structures;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<NotificationParameters, Dictionary<string, string>> invite =
                (corradeNotificationParameters, notificationData) =>
                {
                    var notificationGroupInviteEventArgs =
                        (InstantMessageEventArgs)corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification != null && corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                            CSV.FromEnumerable(
                                wasOpenMetaverse.Reflection.GetStructuredData(notificationGroupInviteEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }

                    GroupInvite groupInvite;
                    lock (GroupInvitesLock)
                    {
                        GroupInvites.TryGetValue(notificationGroupInviteEventArgs.IM.IMSessionID, out groupInvite);
                    }

                    var LockObject = new object();
                    Helpers.Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                        .NotificationParameters.AsParallel()
                        .ForAll(o => o.Value.AsParallel().ForAll(p =>
                        {
                            p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                new List<object> { notificationGroupInviteEventArgs, groupInvite },
                                notificationData, LockObject, languageDetector,
                                GroupBayesClassifiers[corradeNotificationParameters.Notification.GroupUUID]);
                        }));
                };
        }
    }
}
