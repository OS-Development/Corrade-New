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
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> regionsayto =
                (corradeNotificationParameters, notificationData) =>
                {
                    ChatEventArgs regionSayToEventArgs = (ChatEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(regionSayToEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE),
                        regionSayToEventArgs.Message);
                    // language detection
                    string profilePath = IO.PathCombine(CORRADE_CONSTANTS.LIBS_DIRECTORY,
                        CORRADE_CONSTANTS.LANGUAGE_PROFILE_FILE);
                    string mostCertainLanguage = @"Unknown";
                    if (File.Exists(profilePath))
                    {
                        Tuple<LanguageInfo, double> detectedLanguage =
                            new RankedLanguageIdentifierFactory().Load(profilePath)
                                .Identify(regionSayToEventArgs.Message)
                                .FirstOrDefault();
                        if (detectedLanguage != null)
                            mostCertainLanguage = detectedLanguage.Item1.Iso639_3;
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.LANGUAGE), mostCertainLanguage);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.OWNER),
                        regionSayToEventArgs.OwnerID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM),
                        regionSayToEventArgs.SourceID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.NAME),
                        regionSayToEventArgs.FromName);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION),
                        regionSayToEventArgs.Position.ToString());
                };
        }
    }
}