///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using Corrade.Constants;
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
                addconfigurationgroup =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.System))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

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
                                .Any(o => string.Equals(o.Name, groupName, StringComparison.OrdinalIgnoreCase) ||
                                          o.UUID.Equals(groupUUID)))
                            throw new Command.ScriptException(Enumerations.ScriptError.GROUP_ALREADY_CONFIGURED);

                        // Fetch group password.
                        var groupSecret = wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SECRET)),
                                corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(groupSecret))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_SECRET_PROVIDED);

                        // Check for SHA1
                        if (!CORRADE_CONSTANTS.SHA1Regex.IsMatch(groupSecret))
                            throw new Command.ScriptException(Enumerations.ScriptError.INVALID_SECRET_PROVIDED);

                        uint groupWorkers;
                        if (!uint.TryParse(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.WORKERS)),
                                    corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture,
                            out groupWorkers))
                            throw new Command.ScriptException(Enumerations.ScriptError.INVALID_WORKERS_PROVIDED);

                        uint groupSchedules;
                        if (!uint.TryParse(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SCHEDULES)),
                                    corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture,
                            out groupSchedules))
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
                            new HashSet<Configuration.Permissions>(CSV.ToEnumerable(wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PERMISSIONS)),
                                        corradeCommandParameters.Message)))
                                .AsParallel()
                                .Select(o =>
                                    Reflection.GetEnumValueFromName<Configuration.Permissions>(o))
                                .Where(o => !o.Equals(default(Configuration.Permissions))));

                        var groupNotifications =
                            new HashSet<Configuration.Notifications>(CSV.ToEnumerable(wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NOTIFICATIONS)),
                                        corradeCommandParameters.Message)))
                                .AsParallel()
                                .Select(o =>
                                    Reflection.GetEnumValueFromName<Configuration.Notifications>(o))
                                .Where(o => !o.Equals(default(Configuration.Notifications))));

                        Locks.ClientInstanceConfigurationLock.EnterWriteLock();
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
                        Locks.ClientInstanceConfigurationLock.ExitWriteLock();

                        lock (ConfigurationFileLock)
                        {
                            try
                            {
                                using (var fileStream = new FileStream(CORRADE_CONSTANTS.CONFIGURATION_FILE,
                                    FileMode.Create, FileAccess.Write, FileShare.None, 16384, true))
                                {
                                    corradeConfiguration.Save(fileStream, ref corradeConfiguration);
                                }
                            }
                            catch (Exception ex)
                            {
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), ex.Message);
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.UNABLE_TO_SAVE_CORRADE_CONFIGURATION);
                            }
                        }
                    };
        }
    }
}