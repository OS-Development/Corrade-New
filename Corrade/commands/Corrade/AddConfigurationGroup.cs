///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using Corrade.Constants;
using CorradeConfiguration;
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
                addconfigurationgroup =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.System))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        }

                        var target = wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                                corradeCommandParameters.Message));

                        var groupName = string.Empty;
                        var groupUUID = UUID.Zero;
                        switch (UUID.TryParse(target, out groupUUID))
                        {
                            case true:
                                if (!Resolvers.GroupUUIDToName(Client, groupUUID, corradeConfiguration.ServicesTimeout,
                                    ref groupName))
                                    throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                                break;
                            default:
                                if (!Resolvers.GroupNameToUUID(Client, target, corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout,
                                    new DecayingAlarm(corradeConfiguration.DataDecayType),
                                    ref groupUUID))
                                    throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                                break;
                        }

                        if (
                            corradeConfiguration.Groups.AsParallel()
                                .Any(o => Strings.StringEquals(o.Name, groupName) || o.UUID.Equals(groupUUID)))
                            throw new Command.ScriptException(Enumerations.ScriptError.GROUP_ALREADY_CONFIGURED);

                        // Fetch group password.
                        var groupSecret = wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SECRET)),
                                corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(groupSecret))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_SECRET_PROVIDED);

                        // Hash group password.
                        switch (Regex.IsMatch(groupSecret, "[a-fA-F0-9]{40}"))
                        {
                            case false:
                                groupSecret = Utils.SHA1String(groupSecret);
                                break;
                        }

                        uint groupWorkers;
                        if (!uint.TryParse(wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.WORKERS)),
                                corradeCommandParameters.Message)), out groupWorkers))
                            throw new Command.ScriptException(Enumerations.ScriptError.INVALID_WORKERS_PROVIDED);

                        uint groupSchedules;
                        if (!uint.TryParse(wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SCHEDULES)),
                                corradeCommandParameters.Message)), out groupSchedules))
                            throw new Command.ScriptException(Enumerations.ScriptError.INVALID_SCHEDULES_PROVIDED);

                        var groupDatabase = wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATABASE)),
                                corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(groupDatabase))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_DATABASE_PATH_PROVIDED);

                        bool groupChatLogEnabled;
                        bool.TryParse(wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LOGS)),
                                corradeCommandParameters.Message)), out groupChatLogEnabled);

                        var groupChatLog = wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PATH)),
                                corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(groupChatLog))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CHATLOG_PATH_PROVIDED);

                        var groupPermissions =
                            new HashSet<Configuration.Permissions>(ParallelEnumerable.Where(CSV.ToEnumerable(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PERMISSIONS)),
                                    corradeCommandParameters.Message)))
                                .AsParallel()
                                .Select(
                                    Reflection.GetEnumValueFromName<Configuration.Permissions>),
                                o => !o.Equals(default(Configuration.Permissions))));

                        var groupNotifications =
                            new HashSet<Configuration.Notifications>(ParallelEnumerable.Where(CSV.ToEnumerable(wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NOTIFICATIONS)),
                                    corradeCommandParameters.Message)))
                                .AsParallel()
                                .Select(
                                    Reflection.GetEnumValueFromName<Configuration.Notifications>),
                                o => !o.Equals(default(Configuration.Notifications))));

                        corradeConfiguration.Groups.Add(new Configuration.Group
                        {
                            Name = groupName,
                            UUID = groupUUID,
                            Password = groupSecret,
                            Workers = groupWorkers,
                            Schedules = groupSchedules,
                            DatabaseFile = groupDatabase,
                            ChatLogEnabled = groupChatLogEnabled,
                            ChatLog = groupChatLog,
                            Permissions = groupPermissions,
                            Notifications = groupNotifications
                        });

                        lock (ConfigurationFileLock)
                        {
                            corradeConfiguration.Save(CORRADE_CONSTANTS.CONFIGURATION_FILE, ref corradeConfiguration);
                        }
                    };
        }
    }
}