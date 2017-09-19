///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Helpers;
using Corrade.Structures;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<NotificationParameters, Dictionary<string, string>> inventory =
                (corradeNotificationParameters, notificationData) =>
                {
                    var inventoryEventType = corradeNotificationParameters.Event.GetType();
                    if (inventoryEventType == typeof(InstantMessageEventArgs))
                    {
                        var instantMessageEventArgs =
                            (InstantMessageEventArgs)corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification != null && corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(
                                    instantMessageEventArgs.GetStructuredData(
                                        CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var objects = new List<object>
                        {
                            instantMessageEventArgs
                        };

                        // Get inventory offer.
                        InventoryOffer inventoryOffer;
                        lock (InventoryOffersLock)
                        {
                            if (InventoryOffers.TryGetValue(instantMessageEventArgs.IM.IMSessionID, out inventoryOffer))
                            {
                                objects.Add(inventoryOffer);
                            }
                        }

                        // Get inventory item.
                        if (instantMessageEventArgs.IM.BinaryBucket.Length.Equals(17))
                        {
                            var itemUUID = new UUID(instantMessageEventArgs.IM.BinaryBucket, 1);
                            Locks.ClientInstanceInventoryLock.EnterReadLock();
                            if (Client.Inventory.Store.Contains(itemUUID))
                            {
                                objects.Add(Client.Inventory.Store[itemUUID]);
                            }
                            Locks.ClientInstanceInventoryLock.ExitReadLock();
                        }

                        var LockObject = new object();
                        Helpers.Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .Where(o => o.Key.Equals(typeof(InstantMessageEventArgs).FullName))
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    objects,
                                    notificationData, LockObject, languageDetector,
                                    GroupBayesClassifiers[corradeNotificationParameters.Notification.GroupUUID]);
                            }));
                    }

                    if (inventoryEventType == typeof(InventoryObjectOfferedEventArgs))
                    {
                        var inventoryObjectOfferedEventArgs =
                            (InventoryObjectOfferedEventArgs)corradeNotificationParameters.Event;
                        // In case we should send specific data then query the structure and return.
                        if (corradeNotificationParameters.Notification != null && corradeNotificationParameters.Notification.Data != null &&
                            corradeNotificationParameters.Notification.Data.Any())
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                CSV.FromEnumerable(
                                    inventoryObjectOfferedEventArgs.GetStructuredData(
                                        CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }

                        var objects = new List<object>
                        {
                            inventoryObjectOfferedEventArgs
                        };

                        // Get inventory offer.
                        InventoryOffer inventoryOffer;
                        lock (InventoryOffersLock)
                        {
                            if (InventoryOffers.TryGetValue(inventoryObjectOfferedEventArgs.Offer.IMSessionID,
                                out inventoryOffer))
                            {
                                objects.Add(inventoryOffer);
                            }
                        }

                        if (inventoryObjectOfferedEventArgs.Offer.BinaryBucket.Length.Equals(17))
                        {
                            var itemUUID = new UUID(inventoryObjectOfferedEventArgs.Offer.BinaryBucket, 1);
                            Locks.ClientInstanceInventoryLock.EnterReadLock();
                            if (Client.Inventory.Store.Contains(itemUUID))
                            {
                                objects.Add(Client.Inventory.Store[itemUUID]);
                            }
                            Locks.ClientInstanceInventoryLock.ExitReadLock();
                        }

                        var LockObject = new object();
                        Helpers.Notifications.LoadSerializedNotificationParameters(corradeNotificationParameters.Type)
                            .NotificationParameters.AsParallel()
                            .Where(o => o.Key.Equals(typeof(InventoryObjectOfferedEventArgs).FullName))
                            .ForAll(o => o.Value.AsParallel().ForAll(p =>
                            {
                                p.ProcessParameters(Client, corradeConfiguration, o.Key,
                                    objects,
                                    notificationData, LockObject, languageDetector,
                                    GroupBayesClassifiers[corradeNotificationParameters.Notification.GroupUUID]);
                            }));
                    }
                };
        }
    }
}
