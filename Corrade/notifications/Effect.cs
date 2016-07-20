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
        public static partial class CorradeNotifications
        {
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> effect =
                (corradeNotificationParameters, notificationData) =>
                {
                    var viewerEffectType = corradeNotificationParameters.Event.GetType();
                    if (viewerEffectType == typeof (ViewerEffectEventArgs))
                    {
                        var notificationViewerEffectEventArgs =
                            (ViewerEffectEventArgs) corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                                CSV.FromEnumerable(GetStructuredData(notificationViewerEffectEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.EFFECT),
                            notificationViewerEffectEventArgs.Type.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.SOURCE),
                            notificationViewerEffectEventArgs.SourceID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.TARGET),
                            notificationViewerEffectEventArgs.TargetID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION),
                            notificationViewerEffectEventArgs.TargetPosition.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DURATION),
                            notificationViewerEffectEventArgs.Duration.ToString(
                                Utils.EnUsCulture));
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ID),
                            notificationViewerEffectEventArgs.EffectID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                            Reflection.GetNameFromEnumValue(Action.GENERIC));
                        return;
                    }
                    if (viewerEffectType == typeof (ViewerEffectPointAtEventArgs))
                    {
                        var notificationViewerPointAtEventArgs =
                            (ViewerEffectPointAtEventArgs) corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                                CSV.FromEnumerable(GetStructuredData(notificationViewerPointAtEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.SOURCE),
                            notificationViewerPointAtEventArgs.SourceID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.TARGET),
                            notificationViewerPointAtEventArgs.TargetID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION),
                            notificationViewerPointAtEventArgs.TargetPosition.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DURATION),
                            notificationViewerPointAtEventArgs.Duration.ToString(
                                Utils.EnUsCulture));
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ID),
                            notificationViewerPointAtEventArgs.EffectID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                            Reflection.GetNameFromEnumValue(Action.POINT));
                        return;
                    }
                    if (viewerEffectType == typeof (ViewerEffectLookAtEventArgs))
                    {
                        var notificationViewerLookAtEventArgs =
                            (ViewerEffectLookAtEventArgs) corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                                CSV.FromEnumerable(GetStructuredData(notificationViewerLookAtEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.SOURCE),
                            notificationViewerLookAtEventArgs.SourceID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.TARGET),
                            notificationViewerLookAtEventArgs.TargetID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION),
                            notificationViewerLookAtEventArgs.TargetPosition.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DURATION),
                            notificationViewerLookAtEventArgs.Duration.ToString(
                                Utils.EnUsCulture));
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ID),
                            notificationViewerLookAtEventArgs.EffectID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                            Reflection.GetNameFromEnumValue(Action.LOOK));
                    }
                };
        }
    }
}