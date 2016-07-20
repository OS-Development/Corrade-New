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
            public static Action<CorradeNotificationParameters, Dictionary<string, string>> economy =
                (corradeNotificationParameters, notificationData) =>
                {
                    var notificationMoneyBalanceEventArgs =
                        (MoneyBalanceReplyEventArgs) corradeNotificationParameters.Event;
                    // In case we should send specific data then query the structure and return.
                    if (corradeNotificationParameters.Notification.Data != null &&
                        corradeNotificationParameters.Notification.Data.Any())
                    {
                        notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                            CSV.FromEnumerable(GetStructuredData(notificationMoneyBalanceEventArgs,
                                CSV.FromEnumerable(corradeNotificationParameters.Notification.Data))));
                        return;
                    }
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.BALANCE),
                        notificationMoneyBalanceEventArgs.Balance.ToString(
                            Utils.EnUsCulture));
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DESCRIPTION),
                        notificationMoneyBalanceEventArgs.Description);
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.COMMITTED),
                        notificationMoneyBalanceEventArgs.MetersCommitted.ToString(
                            Utils.EnUsCulture));
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.CREDIT),
                        notificationMoneyBalanceEventArgs.MetersCredit.ToString(
                            Utils.EnUsCulture));
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.SUCCESS),
                        notificationMoneyBalanceEventArgs.Success.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.ID),
                        notificationMoneyBalanceEventArgs.TransactionID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.AMOUNT),
                        notificationMoneyBalanceEventArgs.TransactionInfo.Amount.ToString(
                            Utils.EnUsCulture));
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.TARGET),
                        notificationMoneyBalanceEventArgs.TransactionInfo.DestID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.SOURCE),
                        notificationMoneyBalanceEventArgs.TransactionInfo.SourceID.ToString());
                    notificationData.Add(Reflection.GetNameFromEnumValue(ScriptKeys.TRANSACTION),
                        Enum.GetName(typeof (MoneyTransactionType),
                            notificationMoneyBalanceEventArgs.TransactionInfo.TransactionType));
                };
        }
    }
}