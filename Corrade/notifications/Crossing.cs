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
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> crossing =
                (corradeNotificationParameters, notificationData) =>
                {
                    System.Type regionChangeType = corradeNotificationParameters.Event.GetType();
                    if (regionChangeType == typeof (SimChangedEventArgs))
                    {
                        SimChangedEventArgs simChangedEventArgs =
                            (SimChangedEventArgs) corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                                CSV.FromEnumerable(GetStructuredData(simChangedEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }
                        if (simChangedEventArgs.PreviousSimulator != null)
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.OLD),
                                simChangedEventArgs.PreviousSimulator.Name);
                        }
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.NEW),
                            Client.Network.CurrentSim.Name);
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                            Reflection.GetNameFromEnumValue(Action.CHANGED));
                        return;
                    }
                    if (regionChangeType == typeof (RegionCrossedEventArgs))
                    {
                        RegionCrossedEventArgs regionCrossedEventArgs =
                            (RegionCrossedEventArgs) corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                                CSV.FromEnumerable(GetStructuredData(regionCrossedEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }
                        if (regionCrossedEventArgs.OldSimulator != null)
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.OLD),
                                regionCrossedEventArgs.OldSimulator.Name);
                        }
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.NEW),
                            regionCrossedEventArgs.NewSimulator.Name);
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                            Reflection.GetNameFromEnumValue(Action.CROSSED));
                    }
                };
        }
    }
}