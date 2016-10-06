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
using Corrade.Structures;

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
                        var instantMessageEventArgs =
                            (InstantMessageEventArgs) corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(wasOpenMetaverse.Reflection.GetStructuredData(
                                    instantMessageEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var objects = new List<object>
                        {
                            instantMessageEventArgs
                        };

                        InventoryOffer inventoryOffer = new InventoryOffer();
                        switch (instantMessageEventArgs.IM.Dialog)
                        {
                            case InstantMessageDialog.TaskInventoryOffered:
                            case InstantMessageDialog.InventoryOffered:
                                lock (InventoryOffersLock)
                                {
                                    inventoryOffer =
                                        InventoryOffers.AsParallel()
                                            .FirstOrDefault(
                                                p =>
                                                    p.Args.Offer.IMSessionID.Equals(
                                                        instantMessageEventArgs.IM.IMSessionID));
                                    if (inventoryOffer != null)
                                    {
                                        objects.Add(inventoryOffer.Args);
                                        var groups =
                                            CORRADE_CONSTANTS.InventoryOfferObjectNameRegEx.Match(
                                                string.IsNullOrEmpty(inventoryOffer.Name)
                                                    ? inventoryOffer.Args.Offer.Message
                                                    : inventoryOffer.Name).Groups;
                                        if (groups.Count > 1)
                                        {
                                            objects.Add(groups[1]);
                                        }
                                    }
                                }
                                break;
                        }

                        var LockObject = new object();
                        Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .Where(o => o.Key.Equals(typeof (InstantMessageEventArgs).FullName))
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    objects,
                                    notificationData, LockObject, rankedLanguageIdentifier,
                                    GroupBayesClassifiers[corradeNotificationParameters.Notification.GroupUUID]);
                            }));

                        switch (instantMessageEventArgs.IM.Dialog)
                        {
                            case InstantMessageDialog.TaskInventoryOffered:
                            case InstantMessageDialog.InventoryOffered:
                                if (inventoryOffer != null)
                                {
                                    lock (InventoryOffersLock)
                                    {
                                        InventoryOffers.Remove(inventoryOffer);
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
                            .Where(o => o.Key.Equals(typeof (InventoryObjectOfferedEventArgs).FullName))
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    objects,
                                    notificationData, LockObject, rankedLanguageIdentifier,
                                    GroupBayesClassifiers[corradeNotificationParameters.Notification.GroupUUID]);
                            }));
                    }
                };
        }
    }
}