///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Timers;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getgroupaccountsummarydata =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Group))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        UUID groupUUID;
                        var target = wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                                corradeCommandParameters.Message));
                        switch (string.IsNullOrEmpty(target))
                        {
                            case false:
                                if (!UUID.TryParse(target, out groupUUID) &&
                                    !Resolvers.GroupNameToUUID(Client, target, corradeConfiguration.ServicesTimeout,
                                        corradeConfiguration.DataTimeout,
                                        new DecayingAlarm(corradeConfiguration.DataDecayType), ref groupUUID))
                                    throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                                break;

                            default:
                                groupUUID = corradeCommandParameters.Group.UUID;
                                break;
                        }
                        uint days;
                        if (
                            !uint.TryParse(
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DAYS)),
                                    corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture,
                                out days))
                            throw new Command.ScriptException(Enumerations.ScriptError.INVALID_DAYS);
                        uint interval;
                        if (
                            !uint.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.INTERVAL)),
                                        corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture,
                                out interval))
                            throw new Command.ScriptException(Enumerations.ScriptError.INVALID_INTERVAL);
                        var RequestGroupAccountSummaryEvent = new ManualResetEventSlim(false);
                        var summary = new GroupAccountSummary();
                        EventHandler<GroupAccountSummaryReplyEventArgs> RequestGroupAccountSummaryEventHandler =
                            (sender, args) =>
                            {
                                if (!args.GroupID.Equals(groupUUID) ||
                                    !args.Summary.CurrentInterval.Equals(interval) ||
                                    !args.Summary.IntervalDays.Equals(days))
                                    return;
                                summary = args.Summary;
                                RequestGroupAccountSummaryEvent.Set();
                            };
                        Locks.ClientInstanceGroupsLock.EnterReadLock();
                        Client.Groups.GroupAccountSummaryReply += RequestGroupAccountSummaryEventHandler;
                        Client.Groups.RequestGroupAccountSummary(groupUUID, (int) days, (int) interval);
                        if (
                            !RequestGroupAccountSummaryEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                        {
                            Client.Groups.GroupAccountSummaryReply -= RequestGroupAccountSummaryEventHandler;
                            Locks.ClientInstanceGroupsLock.ExitReadLock();
                            throw new Command.ScriptException(
                                Enumerations.ScriptError.TIMEOUT_GETTING_GROUP_ACCOUNT_SUMMARY);
                        }
                        Client.Groups.GroupAccountSummaryReply -= RequestGroupAccountSummaryEventHandler;
                        Locks.ClientInstanceGroupsLock.ExitReadLock();
                        var data =
                            summary.GetStructuredData(
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))).ToList();
                        if (data.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(data));
                    };
        }
    }
}