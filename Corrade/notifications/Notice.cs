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
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> notice =
                (corradeNotificationParameters, notificationData) =>
                {
                    InstantMessageEventArgs notificationGroupNoticeEventArgs =
                        (InstantMessageEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(notificationGroupNoticeEventArgs,
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
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.GROUP),
                        notice.Group.Name);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.SESSION),
                        notice.Session.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT),
                        notice.Agent.UUID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME),
                        notice.Agent.FirstName);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME),
                        notice.Agent.LastName);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.SUBJECT),
                        notice.Subject);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE),
                        notice.Message);
                    // language detection
                    string profilePath = IO.PathCombine(CORRADE_CONSTANTS.LIBS_DIRECTORY,
                        CORRADE_CONSTANTS.LANGUAGE_PROFILE_FILE);
                    string mostCertainLanguage = @"Unknown";
                    if (File.Exists(profilePath))
                    {
                        Tuple<LanguageInfo, double> detectedLanguage =
                            new RankedLanguageIdentifierFactory().Load(profilePath)
                                .Identify(notice.Message)
                                .FirstOrDefault();
                        if (detectedLanguage != null)
                            mostCertainLanguage = detectedLanguage.Item1.Iso639_3;
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.LANGUAGE), mostCertainLanguage);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ATTACHMENTS),
                        notice.Attachment.ToString());
                    if (notice.Attachment)
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ASSET),
                            notice.Asset.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.FOLDER),
                            notice.Folder.ToString());
                    }
                };
        }
    }
}