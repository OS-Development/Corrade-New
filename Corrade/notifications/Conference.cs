///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Helpers = wasOpenMetaverse.Helpers;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> conference =
                (corradeNotificationParameters, notificationData) =>
                {
                    var conferenceMessageEventArgs =
                        (InstantMessageEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(conferenceMessageEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    // Conference name.
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.NAME),
                        Utils.BytesToString(conferenceMessageEventArgs.IM.BinaryBucket));
                    // Conference session.
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.SESSION),
                        conferenceMessageEventArgs.IM.IMSessionID.ToString());
                    Conference conference;
                    lock (ConferencesLock)
                    {
                        conference =
                            Conferences.AsParallel()
                                .FirstOrDefault(o => o.Session.Equals(conferenceMessageEventArgs.IM.IMSessionID));
                    }
                    if (!conference.Equals(default(Conference)))
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.RESTORED),
                            conference.Restored.ToString());
                    }
                    // Avatar name sending the message.
                    var name =
                        Helpers.GetAvatarNames(conferenceMessageEventArgs.IM.FromAgentName);
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
                        conferenceMessageEventArgs.IM.FromAgentID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE),
                        conferenceMessageEventArgs.IM.Message);
                    // language detection
                    var detectedLanguage =
                        rankedLanguageIdentifier.Identify(conferenceMessageEventArgs.IM.Message).FirstOrDefault();
                    if (detectedLanguage != null)
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.LANGUAGE),
                            detectedLanguage.Item1.Iso639_3);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATE),
                        DateTime.Now.ToUniversalTime().ToString(Constants.LSL.DATE_TIME_STAMP));
                };
        }
    }
}