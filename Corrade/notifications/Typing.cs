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
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> typing =
                (corradeNotificationParameters, notificationData) =>
                {
                    InstantMessageEventArgs notificationTypingMessageEventArgs =
                        (InstantMessageEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(notificationTypingMessageEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    IEnumerable<string> name =
                        GetAvatarNames(notificationTypingMessageEventArgs.IM.FromAgentName);
                    if (name != null)
                    {
                        List<string> fullName = new List<string>(name);
                        if (fullName.Count.Equals(2))
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME),
                                fullName.First());
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME),
                                fullName.Last());
                        }
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT),
                        notificationTypingMessageEventArgs.IM.FromAgentID.ToString());
                    switch (notificationTypingMessageEventArgs.IM.Dialog)
                    {
                        case InstantMessageDialog.StartTyping:
                        case InstantMessageDialog.StopTyping:
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                                !notificationTypingMessageEventArgs.IM.Dialog.Equals(
                                    InstantMessageDialog.StartTyping)
                                    ? Reflection.GetNameFromEnumValue(Action.STOP)
                                    : Reflection.GetNameFromEnumValue(Action.START));
                            break;
                    }
                };
        }
    }
}