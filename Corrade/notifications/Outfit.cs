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
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> outfit =
                (corradeNotificationParameters, notificationData) =>
                {
                    OutfitEventArgs outfitEventArgs =
                        (OutfitEventArgs)corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(outfitEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                        outfitEventArgs.Action.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ASSET),
                        outfitEventArgs.Asset.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.CREATOR),
                        outfitEventArgs.Creator.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DESCRIPTION),
                        outfitEventArgs.Description);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.INVENTORY),
                        outfitEventArgs.Inventory.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM),
                        outfitEventArgs.Item.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.NAME),
                        outfitEventArgs.Name);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.PERMISSIONS),
                        outfitEventArgs.Permissions);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.REPLACE),
                        outfitEventArgs.Replace.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY),
                        outfitEventArgs.Entity.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.SLOT),
                        outfitEventArgs.Slot);
                };
        }
    }
}