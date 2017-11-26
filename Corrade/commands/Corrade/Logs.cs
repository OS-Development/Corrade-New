///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Corrade.Constants;
using Corrade.Structures;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasSharp;
using InstantMessage = Corrade.Structures.InstantMessage;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> logs =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int) Configuration.Permissions.Talk))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var LockObject = new object();
                    var csv = new List<string>();
                    switch (Reflection.GetEnumValueFromName<Enumerations.Entity>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message))
                    ))
                    {
                        case Enumerations.Entity.GROUP:
                            // read the log file
                            string groupChatLog;
                            try
                            {
                                lock (GroupLogFileLock)
                                {
                                    using (
                                        var fileStream = new FileStream(corradeCommandParameters.Group.ChatLog,
                                            FileMode.Open, FileAccess.Read, FileShare.Read, 16384, true))
                                    {
                                        using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                                        {
                                            groupChatLog = streamReader.ReadToEnd();
                                        }
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.FAILED_TO_READ_LOG_FILE);
                            }
                            // process the log file and create the set of messages to process
                            var groupMessages = new HashSet<GroupMessage>();
                            groupChatLog.Split(new[] {Environment.NewLine}, StringSplitOptions.None)
                                .AsParallel()
                                .ForAll(
                                    o =>
                                    {
                                        var match = CORRADE_CONSTANTS.GroupMessageLogRegex.Match(o);
                                        if (!match.Success) return;
                                        DateTime messageDateTime;
                                        if (!DateTime.TryParse(match.Groups[1].Value, out messageDateTime)) return;
                                        var messageFirstName = match.Groups[2].Value;
                                        if (string.IsNullOrEmpty(messageFirstName)) return;
                                        var messageLastName = match.Groups[3].Value;
                                        if (string.IsNullOrEmpty(messageLastName)) return;
                                        var message = match.Groups[4].Value;
                                        lock (LockObject)
                                        {
                                            groupMessages.Add(new GroupMessage
                                            {
                                                DateTime = messageDateTime,
                                                FirstName = messageFirstName,
                                                LastName = messageLastName,
                                                Message = message
                                            });
                                        }
                                    });
                            switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                        corradeCommandParameters.Message))
                            ))
                            {
                                case Enumerations.Action.GET:
                                    // search by date
                                    DateTime getGroupMessageFromDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FROM)),
                                        corradeCommandParameters.Message), out getGroupMessageFromDate))
                                        getGroupMessageFromDate = DateTime.MinValue;
                                    DateTime getGroupMessageToDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TO)),
                                        corradeCommandParameters.Message), out getGroupMessageToDate))
                                        getGroupMessageToDate = DateTime.MaxValue;
                                    // build regular expressions based on fed data
                                    Regex getGroupMessageFirstNameRegex;
                                    try
                                    {
                                        getGroupMessageFirstNameRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                                    .FIRSTNAME)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                    }
                                    catch (Exception)
                                    {
                                        getGroupMessageFirstNameRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    Regex getGroupMessageLastNameRegex;
                                    try
                                    {
                                        getGroupMessageLastNameRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                    }
                                    catch (Exception)
                                    {
                                        getGroupMessageLastNameRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    Regex getGroupMessageMessageRegex;
                                    try
                                    {
                                        getGroupMessageMessageRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MESSAGE)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        getGroupMessageMessageRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    // cull the message list depending on what parameters have been specified
                                    groupMessages.AsParallel()
                                        .Where(
                                            o =>
                                                (getGroupMessageToDate >= o.DateTime ||
                                                 getGroupMessageFromDate <= o.DateTime) &&
                                                getGroupMessageFirstNameRegex.IsMatch(o.FirstName) &&
                                                getGroupMessageLastNameRegex.IsMatch(o.LastName) &&
                                                getGroupMessageMessageRegex.IsMatch(o.Message)).ForAll(o =>
                                        {
                                            lock (LockObject)
                                            {
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.TIME),
                                                    o.DateTime.ToUniversalTime()
                                                        .ToString(wasOpenMetaverse.Constants.LSL.DATE_TIME_STAMP)
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME),
                                                    o.FirstName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME),
                                                    o.LastName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.MESSAGE),
                                                    o.Message
                                                });
                                            }
                                        });
                                    break;

                                case Enumerations.Action.SEARCH:
                                    // build regular expressions based on fed data
                                    Regex searchGroupMessagesRegex;
                                    try
                                    {
                                        searchGroupMessagesRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.COULD_NOT_COMPILE_REGULAR_EXPRESSION);
                                    }
                                    groupMessages.AsParallel()
                                        .Where(o => searchGroupMessagesRegex.IsMatch(o.FirstName) ||
                                                    searchGroupMessagesRegex.IsMatch(o.LastName) ||
                                                    searchGroupMessagesRegex.IsMatch(o.Message)).ForAll(o =>
                                        {
                                            lock (LockObject)
                                            {
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.TIME),
                                                    o.DateTime.ToUniversalTime()
                                                        .ToString(
                                                            wasOpenMetaverse.Constants.LSL.DATE_TIME_STAMP)
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.FIRSTNAME),
                                                    o.FirstName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.LASTNAME),
                                                    o.LastName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.MESSAGE),
                                                    o.Message
                                                });
                                            }
                                        });
                                    break;

                                default:
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                            }
                            break;

                        case Enumerations.Entity.MESSAGE:
                            var instantMessages = new HashSet<InstantMessage>();
                            Directory.CreateDirectory(corradeConfiguration.InstantMessageLogDirectory);
                            Directory.EnumerateFiles(corradeConfiguration.InstantMessageLogDirectory).AsParallel()
                                .ForAll(o =>
                                {
                                    string messageLine;
                                    lock (InstantMessageLogFileLock)
                                    {
                                        using (
                                            var fileStream = new FileStream(o, FileMode.Open, FileAccess.Read,
                                                FileShare.Read, 16384, true))
                                        {
                                            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                                            {
                                                messageLine = streamReader.ReadToEnd();
                                            }
                                        }
                                    }
                                    if (string.IsNullOrEmpty(messageLine)) return;
                                    messageLine.Split(new[] {Environment.NewLine}, StringSplitOptions.None)
                                        .AsParallel()
                                        .ForAll(
                                            p =>
                                            {
                                                var match = CORRADE_CONSTANTS.InstantMessageLogRegex.Match(p);
                                                if (!match.Success) return;
                                                DateTime messageDateTime;
                                                if (!DateTime.TryParse(match.Groups[1].Value, out messageDateTime))
                                                    return;
                                                var messageFirstName = match.Groups[2].Value;
                                                if (string.IsNullOrEmpty(messageFirstName)) return;
                                                var messageLastName = match.Groups[3].Value;
                                                if (string.IsNullOrEmpty(messageLastName)) return;
                                                var message = match.Groups[4].Value;
                                                lock (LockObject)
                                                {
                                                    instantMessages.Add(new InstantMessage
                                                    {
                                                        DateTime = messageDateTime,
                                                        FirstName = messageFirstName,
                                                        LastName = messageLastName,
                                                        Message = message
                                                    });
                                                }
                                            });
                                });
                            switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                        corradeCommandParameters.Message))
                            ))
                            {
                                case Enumerations.Action.GET:
                                    // search by date
                                    DateTime getInstantMessageFromDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FROM)),
                                        corradeCommandParameters.Message), out getInstantMessageFromDate))
                                        getInstantMessageFromDate = DateTime.MinValue;
                                    DateTime getInstantMessageToDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TO)),
                                        corradeCommandParameters.Message), out getInstantMessageToDate))
                                        getInstantMessageToDate = DateTime.MaxValue;
                                    // build regular expressions based on fed data
                                    Regex getInstantMessageFirstNameRegex;
                                    try
                                    {
                                        getInstantMessageFirstNameRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                                    .FIRSTNAME)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                    }
                                    catch (Exception)
                                    {
                                        getInstantMessageFirstNameRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    Regex getInstantMessageLastNameRegex;
                                    try
                                    {
                                        getInstantMessageLastNameRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                    }
                                    catch (Exception)
                                    {
                                        getInstantMessageLastNameRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    Regex getInstantMessageMessageRegex;
                                    try
                                    {
                                        getInstantMessageMessageRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MESSAGE)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        getInstantMessageMessageRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    // cull the message list depending on what parameters have been specified
                                    instantMessages.AsParallel().Where(o => (getInstantMessageToDate >= o.DateTime ||
                                                                             getInstantMessageFromDate <= o.DateTime) &&
                                                                            getInstantMessageFirstNameRegex.IsMatch(
                                                                                o.FirstName) &&
                                                                            getInstantMessageLastNameRegex.IsMatch(
                                                                                o.LastName) &&
                                                                            getInstantMessageMessageRegex.IsMatch(
                                                                                o.Message)).ForAll(o =>
                                    {
                                        lock (LockObject)
                                        {
                                            csv.AddRange(new[]
                                            {
                                                Reflection
                                                    .GetNameFromEnumValue(
                                                        Command.ScriptKeys
                                                            .TIME),
                                                o.DateTime.ToUniversalTime()
                                                    .ToString(
                                                        wasOpenMetaverse.Constants.LSL
                                                            .DATE_TIME_STAMP)
                                            });
                                            csv.AddRange(new[]
                                            {
                                                Reflection
                                                    .GetNameFromEnumValue(
                                                        Command.ScriptKeys
                                                            .FIRSTNAME),
                                                o.FirstName
                                            });
                                            csv.AddRange(new[]
                                            {
                                                Reflection
                                                    .GetNameFromEnumValue(
                                                        Command.ScriptKeys
                                                            .LASTNAME),
                                                o.LastName
                                            });
                                            csv.AddRange(new[]
                                            {
                                                Reflection
                                                    .GetNameFromEnumValue(
                                                        Command.ScriptKeys
                                                            .MESSAGE),
                                                o.Message
                                            });
                                        }
                                    });
                                    break;

                                case Enumerations.Action.SEARCH:
                                    // build regular expressions based on fed data
                                    Regex searchInstantMessagesRegex;
                                    try
                                    {
                                        searchInstantMessagesRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.COULD_NOT_COMPILE_REGULAR_EXPRESSION);
                                    }
                                    instantMessages.AsParallel()
                                        .Where(o => searchInstantMessagesRegex.IsMatch(o.FirstName) ||
                                                    searchInstantMessagesRegex.IsMatch(o.LastName) ||
                                                    searchInstantMessagesRegex.IsMatch(o.Message)).ForAll(o =>
                                        {
                                            lock (LockObject)
                                            {
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.TIME),
                                                    o.DateTime.ToUniversalTime()
                                                        .ToString(
                                                            wasOpenMetaverse.Constants.LSL.DATE_TIME_STAMP)
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.FIRSTNAME),
                                                    o.FirstName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.LASTNAME),
                                                    o.LastName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.MESSAGE),
                                                    o.Message
                                                });
                                            }
                                        });
                                    break;

                                default:
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                            }
                            break;

                        case Enumerations.Entity.CONFERENCE:
                            var conferenceMessages = new HashSet<InstantMessage>();
                            Directory.CreateDirectory(corradeConfiguration.ConferenceMessageLogDirectory);
                            Directory.EnumerateFiles(corradeConfiguration.ConferenceMessageLogDirectory)
                                .AsParallel()
                                .ForAll(o =>
                                {
                                    string messageLine;
                                    lock (ConferenceMessageLogFileLock)
                                    {
                                        using (
                                            var fileStream = new FileStream(o, FileMode.Open, FileAccess.Read,
                                                FileShare.Read, 16384, true))
                                        {
                                            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                                            {
                                                messageLine = streamReader.ReadToEnd();
                                            }
                                        }
                                    }
                                    if (string.IsNullOrEmpty(messageLine)) return;
                                    messageLine.Split(new[] {Environment.NewLine}, StringSplitOptions.None)
                                        .AsParallel()
                                        .ForAll(
                                            p =>
                                            {
                                                var match = CORRADE_CONSTANTS.ConferenceMessageLogRegex.Match(p);
                                                if (!match.Success) return;
                                                DateTime messageDateTime;
                                                if (!DateTime.TryParse(match.Groups[1].Value, out messageDateTime))
                                                    return;
                                                var messageFirstName = match.Groups[2].Value;
                                                if (string.IsNullOrEmpty(messageFirstName)) return;
                                                var messageLastName = match.Groups[3].Value;
                                                if (string.IsNullOrEmpty(messageLastName)) return;
                                                var message = match.Groups[4].Value;
                                                lock (LockObject)
                                                {
                                                    conferenceMessages.Add(new InstantMessage
                                                    {
                                                        DateTime = messageDateTime,
                                                        FirstName = messageFirstName,
                                                        LastName = messageLastName,
                                                        Message = message
                                                    });
                                                }
                                            });
                                });
                            switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                        corradeCommandParameters.Message))
                            ))
                            {
                                case Enumerations.Action.GET:
                                    // search by date
                                    DateTime getConferenceMessageFromDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FROM)),
                                        corradeCommandParameters.Message), out getConferenceMessageFromDate))
                                        getConferenceMessageFromDate = DateTime.MinValue;
                                    DateTime getConferenceMessageToDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TO)),
                                        corradeCommandParameters.Message), out getConferenceMessageToDate))
                                        getConferenceMessageToDate = DateTime.MaxValue;
                                    // build regular expressions based on fed data
                                    Regex getConferenceMessageFirstNameRegex;
                                    try
                                    {
                                        getConferenceMessageFirstNameRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                                    .FIRSTNAME)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                    }
                                    catch (Exception)
                                    {
                                        getConferenceMessageFirstNameRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    Regex getConferenceMessageLastNameRegex;
                                    try
                                    {
                                        getConferenceMessageLastNameRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                    }
                                    catch (Exception)
                                    {
                                        getConferenceMessageLastNameRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    Regex getConferenceMessageMessageRegex;
                                    try
                                    {
                                        getConferenceMessageMessageRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MESSAGE)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        getConferenceMessageMessageRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    // cull the message list depending on what parameters have been specified
                                    conferenceMessages.AsParallel()
                                        .Where(o => (getConferenceMessageToDate >= o.DateTime ||
                                                     getConferenceMessageFromDate <= o.DateTime) &&
                                                    getConferenceMessageFirstNameRegex.IsMatch(
                                                        o.FirstName) &&
                                                    getConferenceMessageLastNameRegex.IsMatch(
                                                        o.LastName) &&
                                                    getConferenceMessageMessageRegex.IsMatch(
                                                        o.Message)).ForAll(o =>
                                        {
                                            lock (LockObject)
                                            {
                                                csv.AddRange(new[]
                                                {
                                                    Reflection
                                                        .GetNameFromEnumValue(
                                                            Command.ScriptKeys.TIME),
                                                    o.DateTime.ToUniversalTime()
                                                        .ToString(
                                                            wasOpenMetaverse.Constants.LSL
                                                                .DATE_TIME_STAMP)
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection
                                                        .GetNameFromEnumValue(
                                                            Command.ScriptKeys.FIRSTNAME),
                                                    o.FirstName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection
                                                        .GetNameFromEnumValue(
                                                            Command.ScriptKeys.LASTNAME),
                                                    o.LastName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection
                                                        .GetNameFromEnumValue(
                                                            Command.ScriptKeys.MESSAGE),
                                                    o.Message
                                                });
                                            }
                                        });
                                    break;

                                case Enumerations.Action.SEARCH:
                                    // build regular expressions based on fed data
                                    Regex searchConferenceMessagesRegex;
                                    try
                                    {
                                        searchConferenceMessagesRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.COULD_NOT_COMPILE_REGULAR_EXPRESSION);
                                    }
                                    conferenceMessages.AsParallel()
                                        .Where(o => searchConferenceMessagesRegex.IsMatch(o.FirstName) ||
                                                    searchConferenceMessagesRegex.IsMatch(o.LastName) ||
                                                    searchConferenceMessagesRegex.IsMatch(o.Message)).ForAll(o =>
                                        {
                                            lock (LockObject)
                                            {
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.TIME),
                                                    o.DateTime.ToUniversalTime()
                                                        .ToString(
                                                            wasOpenMetaverse.Constants.LSL.DATE_TIME_STAMP)
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.FIRSTNAME),
                                                    o.FirstName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.LASTNAME),
                                                    o.LastName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.MESSAGE),
                                                    o.Message
                                                });
                                            }
                                        });
                                    break;

                                default:
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                            }
                            break;

                        case Enumerations.Entity.LOCAL:
                            var localMessages = new HashSet<LocalMessage>();
                            Directory.CreateDirectory(corradeConfiguration.LocalMessageLogDirectory);
                            Directory.EnumerateFiles(corradeConfiguration.LocalMessageLogDirectory).AsParallel().ForAll(
                                o =>
                                {
                                    string messageLine;
                                    lock (LocalLogFileLock)
                                    {
                                        using (
                                            var fileStream = new FileStream(o, FileMode.Open, FileAccess.Read,
                                                FileShare.Read, 16384, true))
                                        {
                                            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                                            {
                                                messageLine = streamReader.ReadToEnd();
                                            }
                                        }
                                    }
                                    if (string.IsNullOrEmpty(messageLine)) return;
                                    messageLine.Split(new[] {Environment.NewLine}, StringSplitOptions.None)
                                        .AsParallel()
                                        .ForAll(
                                            p =>
                                            {
                                                var match = CORRADE_CONSTANTS.LocalMessageLogRegex.Match(p);
                                                if (!match.Success) return;
                                                DateTime messageDateTime;
                                                if (!DateTime.TryParse(match.Groups[1].Value, out messageDateTime))
                                                    return;
                                                var messageFirstName = match.Groups[2].Value;
                                                if (string.IsNullOrEmpty(messageFirstName)) return;
                                                var messageLastName = match.Groups[3].Value;
                                                if (string.IsNullOrEmpty(messageLastName)) return;
                                                ChatType messageType;
                                                if (!Enum.TryParse(match.Groups[4].Value, out messageType)) return;
                                                var message = match.Groups[5].Value;
                                                lock (LockObject)
                                                {
                                                    localMessages.Add(new LocalMessage
                                                    {
                                                        DateTime = messageDateTime,
                                                        FirstName = messageFirstName,
                                                        LastName = messageLastName,
                                                        ChatType = messageType,
                                                        Message = message,
                                                        RegionName = Path.GetFileNameWithoutExtension(o)
                                                    });
                                                }
                                            });
                                });
                            switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                        corradeCommandParameters.Message))
                            ))
                            {
                                case Enumerations.Action.GET:
                                    // search by date
                                    DateTime getLocalMessageFromDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FROM)),
                                        corradeCommandParameters.Message), out getLocalMessageFromDate))
                                        getLocalMessageFromDate = DateTime.MinValue;
                                    DateTime getLocalMessageToDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TO)),
                                        corradeCommandParameters.Message), out getLocalMessageToDate))
                                        getLocalMessageToDate = DateTime.MaxValue;
                                    // build regular expressions based on fed data
                                    Regex getLocalMessageFirstNameRegex;
                                    try
                                    {
                                        getLocalMessageFirstNameRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                                    .FIRSTNAME)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                    }
                                    catch (Exception)
                                    {
                                        getLocalMessageFirstNameRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    Regex getLocalMessageLastNameRegex;
                                    try
                                    {
                                        getLocalMessageLastNameRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                    }
                                    catch (Exception)
                                    {
                                        getLocalMessageLastNameRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    Regex getLocalMessageMessageRegex;
                                    try
                                    {
                                        getLocalMessageMessageRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MESSAGE)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        getLocalMessageMessageRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    Regex getLocalMessageRegionNameRegex;
                                    try
                                    {
                                        getLocalMessageRegionNameRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        getLocalMessageRegionNameRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    Regex getLocalMessageChatTypeRegex;
                                    try
                                    {
                                        getLocalMessageChatTypeRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        getLocalMessageChatTypeRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    // cull the message list depending on what parameters have been specified
                                    localMessages.AsParallel()
                                        .Where(
                                            o =>
                                                (getLocalMessageToDate >= o.DateTime ||
                                                 getLocalMessageFromDate <= o.DateTime) &&
                                                getLocalMessageFirstNameRegex.IsMatch(o.FirstName) &&
                                                getLocalMessageLastNameRegex.IsMatch(o.LastName) &&
                                                getLocalMessageMessageRegex.IsMatch(o.Message) &&
                                                getLocalMessageRegionNameRegex.IsMatch(o.RegionName) &&
                                                getLocalMessageChatTypeRegex.IsMatch(Enum.GetName(typeof(ChatType),
                                                    o.ChatType))).ForAll(o =>
                                        {
                                            lock (LockObject)
                                            {
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.TIME),
                                                    o.DateTime.ToUniversalTime()
                                                        .ToString(
                                                            wasOpenMetaverse.Constants.LSL.DATE_TIME_STAMP)
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.REGION),
                                                    o.RegionName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE),
                                                    Enum.GetName(typeof(ChatType),
                                                        o.ChatType)
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.FIRSTNAME),
                                                    o.FirstName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.LASTNAME),
                                                    o.LastName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.MESSAGE),
                                                    o.Message
                                                });
                                            }
                                        });
                                    break;

                                case Enumerations.Action.SEARCH:
                                    // build regular expressions based on fed data
                                    Regex searchLocalMessagesRegex;
                                    try
                                    {
                                        searchLocalMessagesRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.COULD_NOT_COMPILE_REGULAR_EXPRESSION);
                                    }
                                    localMessages.AsParallel()
                                        .Where(o => searchLocalMessagesRegex.IsMatch(o.FirstName) ||
                                                    searchLocalMessagesRegex.IsMatch(o.LastName) ||
                                                    searchLocalMessagesRegex.IsMatch(o.Message) ||
                                                    searchLocalMessagesRegex.IsMatch(o.RegionName) ||
                                                    searchLocalMessagesRegex.IsMatch(Enum.GetName(typeof(ChatType),
                                                        o.ChatType))).ForAll(o =>
                                        {
                                            lock (LockObject)
                                            {
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.TIME),
                                                    o.DateTime.ToUniversalTime()
                                                        .ToString(
                                                            wasOpenMetaverse.Constants.LSL
                                                                .DATE_TIME_STAMP)
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.REGION),
                                                    o.RegionName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.TYPE),
                                                    Enum.GetName(typeof(ChatType),
                                                        o.ChatType)
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.FIRSTNAME),
                                                    o.FirstName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.LASTNAME),
                                                    o.LastName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.MESSAGE),
                                                    o.Message
                                                });
                                            }
                                        });
                                    break;

                                default:
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                            }
                            break;

                        case Enumerations.Entity.REGION:
                            var regionMessages = new HashSet<RegionMessage>();
                            Directory.CreateDirectory(corradeConfiguration.RegionMessageLogDirectory);
                            Directory.EnumerateFiles(corradeConfiguration.RegionMessageLogDirectory).AsParallel()
                                .ForAll(o =>
                                {
                                    string messageLine;
                                    lock (RegionLogFileLock)
                                    {
                                        using (
                                            var fileStream = new FileStream(o, FileMode.Open, FileAccess.Read,
                                                FileShare.Read, 16384, true))
                                        {
                                            using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                                            {
                                                messageLine = streamReader.ReadToEnd();
                                            }
                                        }
                                    }
                                    if (string.IsNullOrEmpty(messageLine)) return;
                                    messageLine.Split(new[] {Environment.NewLine}, StringSplitOptions.None)
                                        .AsParallel()
                                        .ForAll(
                                            p =>
                                            {
                                                var match = CORRADE_CONSTANTS.RegionMessageLogRegex.Match(p);
                                                if (!match.Success) return;
                                                DateTime messageDateTime;
                                                if (!DateTime.TryParse(match.Groups[1].Value, out messageDateTime))
                                                    return;
                                                var messageFirstName = match.Groups[2].Value;
                                                if (string.IsNullOrEmpty(messageFirstName)) return;
                                                var messageLastName = match.Groups[3].Value;
                                                if (string.IsNullOrEmpty(messageLastName)) return;
                                                var message = match.Groups[4].Value;
                                                lock (LockObject)
                                                {
                                                    regionMessages.Add(new RegionMessage
                                                    {
                                                        DateTime = messageDateTime,
                                                        FirstName = messageFirstName,
                                                        LastName = messageLastName,
                                                        Message = message,
                                                        RegionName = Path.GetFileNameWithoutExtension(o)
                                                    });
                                                }
                                            });
                                });
                            switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                        corradeCommandParameters.Message))
                            ))
                            {
                                case Enumerations.Action.GET:
                                    // search by date
                                    DateTime getRegionMessageFromDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FROM)),
                                        corradeCommandParameters.Message), out getRegionMessageFromDate))
                                        getRegionMessageFromDate = DateTime.MinValue;
                                    DateTime getRegionMessageToDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TO)),
                                        corradeCommandParameters.Message), out getRegionMessageToDate))
                                        getRegionMessageToDate = DateTime.MaxValue;
                                    // build regular expressions based on fed data
                                    Regex getRegionMessageFirstNameRegex;
                                    try
                                    {
                                        getRegionMessageFirstNameRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                                    .FIRSTNAME)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                    }
                                    catch (Exception)
                                    {
                                        getRegionMessageFirstNameRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    Regex getRegionMessageLastNameRegex;
                                    try
                                    {
                                        getRegionMessageLastNameRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled | RegexOptions.IgnoreCase);
                                    }
                                    catch (Exception)
                                    {
                                        getRegionMessageLastNameRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    Regex getRegionMessageMessageRegex;
                                    try
                                    {
                                        getRegionMessageMessageRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MESSAGE)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        getRegionMessageMessageRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    Regex getRegionMessageRegionNameRegex;
                                    try
                                    {
                                        getRegionMessageRegionNameRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        getRegionMessageRegionNameRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    // cull the message list depending on what parameters have been specified
                                    regionMessages.AsParallel()
                                        .Where(
                                            o =>
                                                (getRegionMessageToDate >= o.DateTime ||
                                                 getRegionMessageFromDate <= o.DateTime) &&
                                                getRegionMessageFirstNameRegex.IsMatch(o.FirstName) &&
                                                getRegionMessageLastNameRegex.IsMatch(o.LastName) &&
                                                getRegionMessageMessageRegex.IsMatch(o.Message) &&
                                                getRegionMessageRegionNameRegex.IsMatch(o.RegionName)).ForAll(o =>
                                        {
                                            lock (LockObject)
                                            {
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.TIME),
                                                    o.DateTime.ToUniversalTime()
                                                        .ToString(wasOpenMetaverse.Constants.LSL.DATE_TIME_STAMP)
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION),
                                                    o.RegionName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME),
                                                    o.FirstName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME),
                                                    o.LastName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.MESSAGE),
                                                    o.Message
                                                });
                                            }
                                        });
                                    break;

                                case Enumerations.Action.SEARCH:
                                    // build regular expressions based on fed data
                                    Regex searchRegionMessagesRegex;
                                    try
                                    {
                                        searchRegionMessagesRegex = new Regex(KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                                corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.COULD_NOT_COMPILE_REGULAR_EXPRESSION);
                                    }
                                    regionMessages.AsParallel()
                                        .Where(o => searchRegionMessagesRegex.IsMatch(o.FirstName) ||
                                                    searchRegionMessagesRegex.IsMatch(o.LastName) ||
                                                    searchRegionMessagesRegex.IsMatch(o.Message) ||
                                                    searchRegionMessagesRegex.IsMatch(o.RegionName)).ForAll(o =>
                                        {
                                            lock (LockObject)
                                            {
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.TIME),
                                                    o.DateTime.ToUniversalTime()
                                                        .ToString(
                                                            wasOpenMetaverse.Constants.LSL.DATE_TIME_STAMP)
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.REGION),
                                                    o.RegionName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.FIRSTNAME),
                                                    o.FirstName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.LASTNAME),
                                                    o.LastName
                                                });
                                                csv.AddRange(new[]
                                                {
                                                    Reflection.GetNameFromEnumValue(
                                                        Command.ScriptKeys.MESSAGE),
                                                    o.Message
                                                });
                                            }
                                        });
                                    break;

                                default:
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                            }
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }
                    if (csv.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                };
        }
    }
}