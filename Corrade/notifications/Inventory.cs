///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using OpenMetaverse;
using wasSharp;
using Helpers = wasOpenMetaverse.Helpers;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeNotifications
        {
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> inventory =
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
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                                CSV.FromEnumerable(GetStructuredData(inventoryOfferEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }
                        var inventoryObjectOfferedName =
                            new List<string>(Helpers.AvatarFullNameRegex.Matches(
                                inventoryOfferEventArgs.IM.FromAgentName)
                                .Cast<Match>()
                                .ToDictionary(p => new[]
                                {
                                    p.Groups["first"].Value,
                                    p.Groups["last"].Value
                                })
                                .SelectMany(
                                    p =>
                                        new[]
                                        {
                                            p.Key[0].Trim(),
                                            !string.IsNullOrEmpty(p.Key[1])
                                                ? p.Key[1].Trim()
                                                : string.Empty
                                        }));
                        switch (!string.IsNullOrEmpty(inventoryObjectOfferedName.Last()))
                        {
                            case true:
                                notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME),
                                    inventoryObjectOfferedName.First());
                                notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME),
                                    inventoryObjectOfferedName.Last());
                                break;
                            default:
                                notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.NAME),
                                    inventoryObjectOfferedName.First());
                                break;
                        }
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT),
                            inventoryOfferEventArgs.IM.FromAgentID.ToString());
                        switch (inventoryOfferEventArgs.IM.Dialog)
                        {
                            case InstantMessageDialog.InventoryAccepted:
                                notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                                    Reflection.GetNameFromEnumValue(Action.ACCEPT));
                                break;
                            case InstantMessageDialog.InventoryDeclined:
                                notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                                    Reflection.GetNameFromEnumValue(Action.DECLINE));
                                break;
                            case InstantMessageDialog.TaskInventoryOffered:
                            case InstantMessageDialog.InventoryOffered:
                                lock (InventoryOffersLock)
                                {
                                    var
                                        inventoryObjectOfferedEventArgs =
                                            InventoryOffers.AsParallel().FirstOrDefault(p =>
                                                p.Key.Offer.IMSessionID.Equals(
                                                    inventoryOfferEventArgs.IM.IMSessionID));
                                    if (
                                        !inventoryObjectOfferedEventArgs.Equals(
                                            default(
                                                KeyValuePair
                                                    <InventoryObjectOfferedEventArgs, ManualResetEvent>)))
                                    {
                                        switch (inventoryObjectOfferedEventArgs.Key.Accept)
                                        {
                                            case true:
                                                notificationData.Add(
                                                    Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                                                    Reflection.GetNameFromEnumValue(Action.ACCEPT));
                                                break;
                                            default:
                                                notificationData.Add(
                                                    Reflection.GetNameFromEnumValue(ScriptKeys.ACTION),
                                                    Reflection.GetNameFromEnumValue(Action.DECLINE));
                                                break;
                                        }
                                    }
                                    var groups =
                                        CORRADE_CONSTANTS.InventoryOfferObjectNameRegEx.Match(
                                            inventoryObjectOfferedEventArgs.Key.Offer.Message).Groups;
                                    if (groups.Count > 0)
                                    {
                                        notificationData.Add(
                                            Reflection.GetNameFromEnumValue(ScriptKeys.ITEM),
                                            groups[1].Value);
                                    }
                                    InventoryOffers.Remove(inventoryObjectOfferedEventArgs.Key);
                                }
                                break;
                        }
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DIRECTION),
                            Reflection.GetNameFromEnumValue(Action.REPLY));
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
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                                CSV.FromEnumerable(GetStructuredData(inventoryObjectOfferedEventArgs,
                                    CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                            return;
                        }
                        var inventoryObjectOfferedName =
                            new List<string>(Helpers.AvatarFullNameRegex.Matches(
                                inventoryObjectOfferedEventArgs.Offer.FromAgentName)
                                .Cast<Match>()
                                .ToDictionary(p => new[]
                                {
                                    p.Groups["first"].Value,
                                    p.Groups["last"].Value
                                })
                                .SelectMany(
                                    p =>
                                        new[]
                                        {
                                            p.Key[0],
                                            !string.IsNullOrEmpty(p.Key[1])
                                                ? p.Key[1]
                                                : string.Empty
                                        }));
                        switch (!string.IsNullOrEmpty(inventoryObjectOfferedName.Last()))
                        {
                            case true:
                                notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME),
                                    inventoryObjectOfferedName.First());
                                notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME),
                                    inventoryObjectOfferedName.Last());
                                break;
                            default:
                                notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.NAME),
                                    inventoryObjectOfferedName.First());
                                break;
                        }
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT),
                            inventoryObjectOfferedEventArgs.Offer.FromAgentID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ASSET),
                            inventoryObjectOfferedEventArgs.AssetType.ToString());
                        var groups =
                            CORRADE_CONSTANTS.InventoryOfferObjectNameRegEx.Match(
                                inventoryObjectOfferedEventArgs.Offer.Message).Groups;
                        if (groups.Count > 0)
                        {
                            notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM),
                                groups[1].Value);
                        }
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.SESSION),
                            inventoryObjectOfferedEventArgs.Offer.IMSessionID.ToString());
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DIRECTION),
                            Reflection.GetNameFromEnumValue(Action.OFFER));
                    }
                };
        }
    }
}