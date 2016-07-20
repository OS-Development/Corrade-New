///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> membership =
                (corradeNotificationParameters, notificationData) =>
                {
                    var groupMembershipEventArgs =
                        (GroupMembershipEventArgs) corradeNotificationParameters.Event;
                    // Set-up filters.
                    if (!groupMembershipEventArgs.GroupUUID.Equals(corradeNotificationParameters.Notification.GroupUUID))
                        return;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(groupMembershipEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    var name = Helpers.GetAvatarNames(groupMembershipEventArgs.AgentName);
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
                        groupMembershipEventArgs.AgentUUID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.GROUP),
                        groupMembershipEventArgs.GroupName);
                    switch (groupMembershipEventArgs.Action)
                    {
                        case Action.JOINED:
                        case Action.PARTED:
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                                !groupMembershipEventArgs.Action.Equals(
                                    Action.JOINED)
                                    ? Reflection.GetNameFromEnumValue(Action.PARTED)
                                    : Reflection.GetNameFromEnumValue(Action.JOINED));
                            break;
                    }
                };
        }
    }
}