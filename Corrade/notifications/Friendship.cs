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
using Helpers = wasOpenMetaverse.Helpers;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> friendship =
                (corradeNotificationParameters, notificationData) =>
                {
                    var friendshipNotificationType = corradeNotificationParameters.Event.GetType();
                    if (friendshipNotificationType == typeof (FriendInfoEventArgs))
                    {
                        var friendInfoEventArgs =
                            (FriendInfoEventArgs) corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                                CSV.FromEnumerable(GetStructuredData(friendInfoEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }
                        var name = Helpers.GetAvatarNames(friendInfoEventArgs.Friend.Name);
                        if (name != null)
                        {
                            var fullName = new List<string>(name);
                            if (fullName.Count.Equals(2))
                            {
                                notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME),
                                    fullName.First());
                                notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME),
                                    fullName.Last());
                            }
                        }
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT),
                            friendInfoEventArgs.Friend.UUID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.STATUS),
                            friendInfoEventArgs.Friend.IsOnline
                                ? Reflection.GetNameFromEnumValue(Action.ONLINE)
                                : Reflection.GetNameFromEnumValue(Action.OFFLINE));
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.RIGHTS),
                            // Return the friend rights as a nice CSV string.
                            CSV.FromEnumerable(typeof (FriendRights).GetFields(BindingFlags.Public |
                                                                               BindingFlags.Static)
                                .AsParallel().Where(
                                    p => friendInfoEventArgs.Friend.MyFriendRights.IsMaskFlagSet((FriendRights)p.GetValue(null)))
                                .Select(p => p.Name)));
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                            Reflection.GetNameFromEnumValue(Action.UPDATE));
                        return;
                    }
                    if (friendshipNotificationType == typeof (FriendshipResponseEventArgs))
                    {
                        var friendshipResponseEventArgs =
                            (FriendshipResponseEventArgs) corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                                CSV.FromEnumerable(GetStructuredData(friendshipResponseEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }
                        var name = Helpers.GetAvatarNames(friendshipResponseEventArgs.AgentName);
                        if (name != null)
                        {
                            var fullName = new List<string>(name);
                            if (fullName.Count.Equals(2))
                            {
                                notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME),
                                    fullName.First());
                                notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME),
                                    fullName.Last());
                            }
                        }
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT),
                            friendshipResponseEventArgs.AgentID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                            Reflection.GetNameFromEnumValue(Action.RESPONSE));
                        return;
                    }
                    if (friendshipNotificationType == typeof (FriendshipOfferedEventArgs))
                    {
                        var friendshipOfferedEventArgs =
                            (FriendshipOfferedEventArgs) corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                                CSV.FromEnumerable(GetStructuredData(friendshipOfferedEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }
                        var name = Helpers.GetAvatarNames(friendshipOfferedEventArgs.AgentName);
                        if (name != null)
                        {
                            var fullName = new List<string>(name);
                            if (fullName.Count.Equals(2))
                            {
                                notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME),
                                    fullName.First());
                                notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME),
                                    fullName.Last());
                            }
                        }
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT),
                            friendshipOfferedEventArgs.AgentID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                            Reflection.GetNameFromEnumValue(Action.REQUEST));
                    }
                };
        }
    }
}