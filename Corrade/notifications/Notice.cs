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
using GroupNotice = Corrade.Structures.GroupNotice;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<NotificationParameters, Dictionary<string, string>> notice =
                (corradeNotificationParameters, notificationData) =>
                {
                    var notificationGroupNoticeEventArgs =
                        (InstantMessageEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                            CSV.FromEnumerable(
                                wasOpenMetaverse.Reflection.GetStructuredData(notificationGroupNoticeEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    // Retrieve the stored notice.
                    GroupNotice notice;
                    lock (GroupNoticeLock)
                    {
                        notice = GroupNotices.AsParallel()
                            .FirstOrDefault(
                                o => o.Session.Equals(notificationGroupNoticeEventArgs.IM.IMSessionID));
                    }
                    // If the notice could not be retrieved, then abort.
                    if (notice.Equals(default(GroupNotice))) return;
                    // Only send notices to the same group that requested notifications.
                    if (!notice.Group.ID.Equals(corradeNotificationParameters.Notification.GroupUUID)) return;
                    lock (GroupNoticeLock)
                    {
                        GroupNotices.Remove(notice);
                    }

                    var LockObject = new object();
                    Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                        .NotificationParameters.AsParallel()
                        .ForAll(o => o.Value.AsParallel().ForAll(p =>
                        {
                            p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                new List<object> {notificationGroupNoticeEventArgs, notice},
                                notificationData, LockObject, rankedLanguageIdentifier);
                        }));
                };
        }
    }
}