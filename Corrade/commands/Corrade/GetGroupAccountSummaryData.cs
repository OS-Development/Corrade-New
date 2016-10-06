///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
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
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        }
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
                                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType), ref groupUUID))
                                    throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                                break;
                            default:
                                groupUUID = corradeCommandParameters.Group.UUID;
                                break;
                        }
                        int days;
                        if (
                            !int.TryParse(
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DAYS)),
                                    corradeCommandParameters.Message)),
                                out days))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.INVALID_DAYS);
                        }
                        int interval;
                        if (
                            !int.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.INTERVAL)),
                                        corradeCommandParameters.Message)),
                                out interval))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.INVALID_INTERVAL);
                        }
                        var RequestGroupAccountSummaryEvent = new ManualResetEvent(false);
                        var summary = new GroupAccountSummary();
                        EventHandler<GroupAccountSummaryReplyEventArgs> RequestGroupAccountSummaryEventHandler =
                            (sender, args) =>
                            {
                                summary = args.Summary;
                                RequestGroupAccountSummaryEvent.Set();
                            };
                        lock (Locks.ClientInstanceGroupsLock)
                        {
                            Client.Groups.GroupAccountSummaryReply += RequestGroupAccountSummaryEventHandler;
                            Client.Groups.RequestGroupAccountSummary(groupUUID, days, interval);
                            if (
                                !RequestGroupAccountSummaryEvent.WaitOne((int) corradeConfiguration.ServicesTimeout,
                                    false))
                            {
                                Client.Groups.GroupAccountSummaryReply -= RequestGroupAccountSummaryEventHandler;
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_GETTING_GROUP_ACCOUNT_SUMMARY);
                            }
                            Client.Groups.GroupAccountSummaryReply -= RequestGroupAccountSummaryEventHandler;
                        }
                        var data =
                            summary.GetStructuredData(
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))).ToList();
                        if (data.Any())
                        {
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(data));
                        }
                    };
        }
    }
}