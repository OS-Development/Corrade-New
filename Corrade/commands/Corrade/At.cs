///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> at =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Schedule))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var groupSchedules = new List<Command.GroupSchedule>();
                    uint index;
                    switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                    ))
                    {
                        case Enumerations.Action.ADD:
                            if (
                                GroupSchedules
                                    .AsParallel()
                                    .Count(o => o.Group.Equals(corradeCommandParameters.Group)) +
                                1 > corradeCommandParameters.Group.Schedules)
                                throw new Command.ScriptException(Enumerations.ScriptError.GROUP_SCHEDULES_EXCEEDED);
                            DateTime at;
                            if (!DateTime.TryParse(wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TIME)),
                                        corradeCommandParameters.Message)), CultureInfo.InvariantCulture,
                                DateTimeStyles.AdjustToUniversal, out at))
                                throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_DATE_TIME_STAMP);
                            var data = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                    corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(data))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_DATA_PROVIDED);
                            lock (GroupSchedulesLock)
                            {
                                GroupSchedules.Add(new Command.GroupSchedule
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

                        case Enumerations.Action.GET:
                            if (!uint.TryParse(wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.INDEX)),
                                        corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture,
                                out index))
                                index = 0;
                            lock (GroupSchedulesLock)
                            {
                                groupSchedules.AddRange(GroupSchedules.OrderByDescending(o => o.At));
                            }
                            if (index > groupSchedules.Count - 1)
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_SCHEDULE_FOUND);
                            var groupSchedule = groupSchedules[(int) index];
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(new[]
                                {
                                    groupSchedule.Sender,
                                    groupSchedule.Identifier,
                                    groupSchedule.At.ToString(wasOpenMetaverse.Constants.LSL.DATE_TIME_STAMP),
                                    groupSchedule.Message
                                }));
                            break;

                        case Enumerations.Action.REMOVE:
                            if (!uint.TryParse(wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.INDEX)),
                                        corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture,
                                out index))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_INDEX_PROVIDED);
                            lock (GroupSchedulesLock)
                            {
                                groupSchedules.AddRange(GroupSchedules.OrderByDescending(o => o.At));
                            }
                            if (index > groupSchedules.Count - 1)
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_SCHEDULE_FOUND);
                            // remove by group name, group UUID, scheduled time or command message
                            lock (GroupSchedulesLock)
                            {
                                GroupSchedules.Remove(groupSchedules[(int) index]);
                            }
                            // Save the group schedules state.
                            SaveGroupSchedulesState.Invoke();
                            break;

                        case Enumerations.Action.LIST:
                            var csv = new List<string>();
                            lock (GroupSchedulesLock)
                            {
                                csv.AddRange(GroupSchedules.OrderByDescending(o => o.At)
                                    .SelectMany(
                                        o =>
                                            new[]
                                            {
                                                o.Sender, o.Identifier,
                                                o.At.ToString(wasOpenMetaverse.Constants.LSL.DATE_TIME_STAMP), o.Message
                                            }));
                            }
                            if (csv.Any())
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                    CSV.FromEnumerable(csv));
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}