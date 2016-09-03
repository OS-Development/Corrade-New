///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Corrade.Constants;
using Corrade.Helpers;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<NotificationParameters, Dictionary<string, string>> inventory =
                (corradeNotificationParameters, notificationData) =>
                {
                    var inventoryOfferedType = corradeNotificationParameters.Event.GetType();
                    if (inventoryOfferedType == typeof (InstantMessageEventArgs))
                    {
                        var inventoryOfferEventArgs =
                            (InstantMessageEventArgs) corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(wasOpenMetaverse.Reflection.GetStructuredData(
                                    inventoryOfferEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var objects = new List<object>
                        {
                            inventoryOfferEventArgs
                        };

                        var inventoryObjectOfferedEventArgs =
                            new KeyValuePair<InventoryObjectOfferedEventArgs, ManualResetEvent>();
                        switch (inventoryOfferEventArgs.IM.Dialog)
                        {
                            case InstantMessageDialog.TaskInventoryOffered:
                            case InstantMessageDialog.InventoryOffered:
                                lock (InventoryOffersLock)
                                {
                                    inventoryObjectOfferedEventArgs =
                                        InventoryOffers.AsParallel().FirstOrDefault(p =>
                                            p.Key.Offer.IMSessionID.Equals(
                                                inventoryOfferEventArgs.IM.IMSessionID));
                                    if (
                                        !inventoryObjectOfferedEventArgs.Equals(
                                            default(
                                                KeyValuePair<InventoryObjectOfferedEventArgs, ManualResetEvent>)))
                                    {
                                        objects.Add(inventoryObjectOfferedEventArgs.Key);
                                    }
                                    var groups =
                                        CORRADE_CONSTANTS.InventoryOfferObjectNameRegEx.Match(
                                            inventoryObjectOfferedEventArgs.Key.Offer.Message).Groups;
                                    if (groups.Count > 1)
                                    {
                                        objects.Add(groups[1]);
                                    }
                                }
                                break;
                        }

                        var LockObject = new object();
                        Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    objects,
                                    notificationData, LockObject, rankedLanguageIdentifier);
                            }));

                        switch (inventoryOfferEventArgs.IM.Dialog)
                        {
                            case InstantMessageDialog.TaskInventoryOffered:
                            case InstantMessageDialog.InventoryOffered:
                                if (
                                    !inventoryObjectOfferedEventArgs.Equals(
                                        default(
                                            KeyValuePair<InventoryObjectOfferedEventArgs, ManualResetEvent>)))
                                {
                                    lock (InventoryOffersLock)
                                    {
                                        InventoryOffers.Remove(inventoryObjectOfferedEventArgs.Key);
                                    }
                                }
                                break;
                        }
                        return;
                    }
                    if (inventoryOfferedType == typeof (InventoryObjectOfferedEventArgs))
                    {
                        var inventoryObjectOfferedEventArgs =
                            (InventoryObjectOfferedEventArgs) corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(
                                    wasOpenMetaverse.Reflection.GetStructuredData(inventoryObjectOfferedEventArgs,
                                        CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var objects = new List<object>
                        {
                            inventoryObjectOfferedEventArgs
                        };

                        var groups =
                            CORRADE_CONSTANTS.InventoryOfferObjectNameRegEx.Match(
                                inventoryObjectOfferedEventArgs.Offer.Message).Groups;
                        if (groups.Count > 1)
                        {
                            objects.Add(groups[1]);
                        }

                        var LockObject = new object();
                        Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    objects,
                                    notificationData, LockObject, rankedLanguageIdentifier);
                            }));
                    }
                };
        }
    }
}