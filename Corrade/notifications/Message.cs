///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NTextCat;
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
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> message =
                (corradeNotificationParameters, notificationData) =>
                {
                    InstantMessageEventArgs notificationInstantMessage =
                        (InstantMessageEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(notificationInstantMessage,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    IEnumerable<string> name =
                        Helpers.GetAvatarNames(notificationInstantMessage.IM.FromAgentName);
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
                        notificationInstantMessage.IM.FromAgentID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE),
                        notificationInstantMessage.IM.Message);
                    // language detection
                    string profilePath = IO.PathCombine(CORRADE_CONSTANTS.LIBS_DIRECTORY,
                        CORRADE_CONSTANTS.LANGUAGE_PROFILE_FILE);
                    string mostCertainLanguage = @"Unknown";
                    if (File.Exists(profilePath))
                    {
                        Tuple<LanguageInfo, double> detectedLanguage =
                            new RankedLanguageIdentifierFactory().Load(profilePath)
                                .Identify(notificationInstantMessage.IM.Message)
                                .FirstOrDefault();
                        if (detectedLanguage != null)
                            mostCertainLanguage = detectedLanguage.Item1.Iso639_3;
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.LANGUAGE), mostCertainLanguage);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATE),
                        notificationInstantMessage.IM.Timestamp.ToString(Constants.LSL.DATE_TIME_STAMP));
                };
        }
    }
}