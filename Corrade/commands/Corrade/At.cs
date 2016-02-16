///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> at =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Schedule))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    List<GroupSchedule> groupSchedules = new List<GroupSchedule>();
                    uint index;
                    switch (Reflection.GetEnumValueFromName<Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Action.ADD:
                            if (GroupSchedules.AsParallel().Count(o => o.Group.Equals(corradeCommandParameters.Group)) +
                                1 > corradeCommandParameters.Group.Schedules)
                            {
                                throw new ScriptException(ScriptError.GROUP_SCHEDULES_EXCEEDED);
                            }
                            DateTime at;
                            if (!DateTime.TryParse(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TIME)),
                                    corradeCommandParameters.Message)), out at))
                            {
                                throw new ScriptException(ScriptError.UNKNOWN_DATE_TIME_STAMP);
                            }
                            string data = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(data))
                            {
                                throw new ScriptException(ScriptError.NO_DATA_PROVIDED);
                            }
                            lock (GroupSchedulesLock)
                            {
                                GroupSchedules.Add(new GroupSchedule
                                {
                                    Group = corradeCommandParameters.Group,
                                    At = at,
                                    Sender = corradeCommandParameters.Sender,
                                    Identifier = corradeCommandParameters.Identifier,
                                    Message = data
                                });
                            }
                            // Save the group schedules state.
                            SaveGroupSchedulesState.Invoke();
                            break;
                        case Action.GET:
                            if (!uint.TryParse(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.INDEX)),
                                    corradeCommandParameters.Message)), out index))
                            {
                                index = 0;
                            }
                            lock (GroupSchedulesLock)
                            {
                                groupSchedules.AddRange(GroupSchedules.OrderByDescending(o => o.At));
                            }
                            if (index > groupSchedules.Count - 1)
                            {
                                throw new ScriptException(ScriptError.NO_SCHEDULE_FOUND);
                            }
                            GroupSchedule groupSchedule = groupSchedules[(int) index];
                            result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA), CSV.FromEnumerable(new[]
                            {
                                groupSchedule.Sender,
                                groupSchedule.Identifier,
                                groupSchedule.At.ToString(Constants.LSL.DATE_TIME_STAMP),
                                groupSchedule.Message
                            }));
                            break;
                        case Action.REMOVE:
                            if (!uint.TryParse(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.INDEX)),
                                    corradeCommandParameters.Message)), out index))
                            {
                                throw new ScriptException(ScriptError.NO_INDEX_PROVIDED);
                            }
                            lock (GroupSchedulesLock)
                            {
                                groupSchedules.AddRange(GroupSchedules.OrderByDescending(o => o.At));
                            }
                            if (index > groupSchedules.Count - 1)
                            {
                                throw new ScriptException(ScriptError.NO_SCHEDULE_FOUND);
                            }
                            // remove by group name, group UUID, scheduled time or command message
                            lock (GroupSchedulesLock)
                            {
                                GroupSchedules.Remove(groupSchedules[(int) index]);
                            }
                            // Save the group schedules state.
                            SaveGroupSchedulesState.Invoke();
                            break;
                        case Action.LIST:
                            List<string> csv = new List<string>();
                            lock (GroupSchedulesLock)
                            {
                                csv.AddRange(GroupSchedules.OrderByDescending(o => o.At)
                                    .SelectMany(
                                        o =>
                                            new[]
                                            {
                                                o.Sender, o.Identifier,
                                                o.At.ToString(Constants.LSL.DATE_TIME_STAMP), o.Message
                                            }));
                            }
                            if (csv.Any())
                            {
                                result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                                    CSV.FromEnumerable(csv));
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}