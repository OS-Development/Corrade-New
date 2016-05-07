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
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> logs =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int) Configuration.Permissions.Talk))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    object LockObject = new object();
                    List<string> csv = new List<string>();
                    switch (Reflection.GetEnumValueFromName<Entity>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Entity.GROUP:
                            // read the log file
                            string groupChatLog;
                            try
                            {
                                lock (GroupLogFileLock)
                                {
                                    using (
                                        FileStream fileStream = File.Open(corradeCommandParameters.Group.ChatLog,
                                            FileMode.Open, FileAccess.Read, FileShare.Read))
                                    {
                                        using (StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8))
                                        {
                                            groupChatLog = streamReader.ReadToEnd();
                                        }
                                    }
                                }
                            }
                            catch (Exception)
                            {
                                throw new ScriptException(ScriptError.FAILED_TO_READ_LOG_FILE);
                            }
                            // process the log file and create the set of messages to process
                            HashSet<GroupMessage> groupMessages = new HashSet<GroupMessage>();
                            Parallel.ForEach(groupChatLog.Split(new[] {Environment.NewLine}, StringSplitOptions.None),
                                o =>
                                {
                                    Match match = CORRADE_CONSTANTS.GroupMessageLogRegex.Match(o);
                                    if (!match.Success) return;
                                    DateTime messageDateTime;
                                    if (!DateTime.TryParse(match.Groups[1].Value, out messageDateTime)) return;
                                    string messageFirstName = match.Groups[2].Value;
                                    if (string.IsNullOrEmpty(messageFirstName)) return;
                                    string messageLastName = match.Groups[3].Value;
                                    if (string.IsNullOrEmpty(messageLastName)) return;
                                    string message = match.Groups[4].Value;
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
                            switch (Reflection.GetEnumValueFromName<Action>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                        corradeCommandParameters.Message))
                                    .ToLowerInvariant()))
                            {
                                case Action.GET:
                                    // search by date
                                    DateTime getGroupMessageFromDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FROM)),
                                        corradeCommandParameters.Message), out getGroupMessageFromDate))
                                    {
                                        getGroupMessageFromDate = DateTime.MinValue;
                                    }
                                    DateTime getGroupMessageToDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TO)),
                                        corradeCommandParameters.Message), out getGroupMessageToDate))
                                    {
                                        getGroupMessageToDate = DateTime.MaxValue;
                                    }
                                    // build regular expressions based on fed data
                                    Regex getGroupMessageFirstNameRegex;
                                    try
                                    {
                                        getGroupMessageFirstNameRegex = new Regex(KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME)),
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
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME)),
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
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE)),
                                            corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        getGroupMessageMessageRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    // cull the message list depending on what parameters have been specified
                                    groupMessages.RemoveWhere(
                                        o =>
                                            (getGroupMessageToDate < o.DateTime && getGroupMessageFromDate > o.DateTime) ||
                                            !getGroupMessageFirstNameRegex.IsMatch(o.FirstName) ||
                                            !getGroupMessageLastNameRegex.IsMatch(o.LastName) ||
                                            !getGroupMessageMessageRegex.IsMatch(o.Message));
                                    Parallel.ForEach(groupMessages, o =>
                                    {
                                        lock (LockObject)
                                        {
                                            csv.AddRange(new[]
                                            {
                                                Reflection.GetNameFromEnumValue(ScriptKeys.TIME),
                                                o.DateTime.ToUniversalTime()
                                                    .ToString(Constants.LSL.DATE_TIME_STAMP)
                                            });
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME), o.FirstName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME), o.LastName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE), o.Message});
                                        }
                                    });
                                    break;
                                case Action.SEARCH:
                                    // build regular expressions based on fed data
                                    Regex searchGroupMessagesRegex;
                                    try
                                    {
                                        searchGroupMessagesRegex = new Regex(KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                            corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        throw new ScriptException(ScriptError.COULD_NOT_COMPILE_REGULAR_EXPRESSION);
                                    }
                                    Parallel.ForEach(groupMessages, o =>
                                    {
                                        if (!searchGroupMessagesRegex.IsMatch(o.FirstName) &&
                                            !searchGroupMessagesRegex.IsMatch(o.LastName) &&
                                            !searchGroupMessagesRegex.IsMatch(o.Message)) return;
                                        lock (LockObject)
                                        {
                                            csv.AddRange(new[]
                                            {
                                                Reflection.GetNameFromEnumValue(ScriptKeys.TIME),
                                                o.DateTime.ToUniversalTime()
                                                    .ToString(Constants.LSL.DATE_TIME_STAMP)
                                            });
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME), o.FirstName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME), o.LastName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE), o.Message});
                                        }
                                    });
                                    break;
                                default:
                                    throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                            }
                            break;
                        case Entity.MESSAGE:
                            HashSet<InstantMessage> instantMessages = new HashSet<InstantMessage>();
                            Parallel.ForEach(Directory.GetFiles(corradeConfiguration.InstantMessageLogDirectory), o =>
                            {
                                string messageLine;
                                lock (InstantMessageLogFileLock)
                                {
                                    using (
                                        FileStream fileStream = File.Open(o, FileMode.Open, FileAccess.Read,
                                            FileShare.Read))
                                    {
                                        using (StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8))
                                        {
                                            messageLine = streamReader.ReadToEnd();
                                        }
                                    }
                                }
                                if (string.IsNullOrEmpty(messageLine)) return;
                                Parallel.ForEach(
                                    messageLine.Split(new[] {Environment.NewLine}, StringSplitOptions.None),
                                    p =>
                                    {
                                        Match match = CORRADE_CONSTANTS.InstantMessageLogRegex.Match(p);
                                        if (!match.Success) return;
                                        DateTime messageDateTime;
                                        if (!DateTime.TryParse(match.Groups[1].Value, out messageDateTime)) return;
                                        string messageFirstName = match.Groups[2].Value;
                                        if (string.IsNullOrEmpty(messageFirstName)) return;
                                        string messageLastName = match.Groups[3].Value;
                                        if (string.IsNullOrEmpty(messageLastName)) return;
                                        string message = match.Groups[4].Value;
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
                            switch (Reflection.GetEnumValueFromName<Action>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                        corradeCommandParameters.Message))
                                    .ToLowerInvariant()))
                            {
                                case Action.GET:
                                    // search by date
                                    DateTime getInstantMessageFromDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FROM)),
                                        corradeCommandParameters.Message), out getInstantMessageFromDate))
                                    {
                                        getInstantMessageFromDate = DateTime.MinValue;
                                    }
                                    DateTime getInstantMessageToDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TO)),
                                        corradeCommandParameters.Message), out getInstantMessageToDate))
                                    {
                                        getInstantMessageToDate = DateTime.MaxValue;
                                    }
                                    // build regular expressions based on fed data
                                    Regex getInstantMessageFirstNameRegex;
                                    try
                                    {
                                        getInstantMessageFirstNameRegex = new Regex(KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME)),
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
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME)),
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
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE)),
                                            corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        getInstantMessageMessageRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    // cull the message list depending on what parameters have been specified
                                    instantMessages.RemoveWhere(
                                        o =>
                                            (getInstantMessageToDate < o.DateTime &&
                                             getInstantMessageFromDate > o.DateTime) ||
                                            !getInstantMessageFirstNameRegex.IsMatch(o.FirstName) ||
                                            !getInstantMessageLastNameRegex.IsMatch(o.LastName) ||
                                            !getInstantMessageMessageRegex.IsMatch(o.Message));
                                    Parallel.ForEach(instantMessages, o =>
                                    {
                                        lock (LockObject)
                                        {
                                            csv.AddRange(new[]
                                            {
                                                Reflection.GetNameFromEnumValue(ScriptKeys.TIME),
                                                o.DateTime.ToUniversalTime()
                                                    .ToString(Constants.LSL.DATE_TIME_STAMP)
                                            });
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME), o.FirstName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME), o.LastName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE), o.Message});
                                        }
                                    });
                                    break;
                                case Action.SEARCH:
                                    // build regular expressions based on fed data
                                    Regex searchInstantMessagesRegex;
                                    try
                                    {
                                        searchInstantMessagesRegex = new Regex(KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                            corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        throw new ScriptException(ScriptError.COULD_NOT_COMPILE_REGULAR_EXPRESSION);
                                    }
                                    Parallel.ForEach(instantMessages, o =>
                                    {
                                        if (!searchInstantMessagesRegex.IsMatch(o.FirstName) &&
                                            !searchInstantMessagesRegex.IsMatch(o.LastName) &&
                                            !searchInstantMessagesRegex.IsMatch(o.Message))
                                            return;
                                        lock (LockObject)
                                        {
                                            csv.AddRange(new[]
                                            {
                                                Reflection.GetNameFromEnumValue(ScriptKeys.TIME),
                                                o.DateTime.ToUniversalTime()
                                                    .ToString(Constants.LSL.DATE_TIME_STAMP)
                                            });
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME), o.FirstName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME), o.LastName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE), o.Message});
                                        }
                                    });
                                    break;
                                default:
                                    throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                            }
                            break;
                        case Entity.LOCAL:
                            HashSet<LocalMessage> localMessages = new HashSet<LocalMessage>();
                            Parallel.ForEach(Directory.GetFiles(corradeConfiguration.LocalMessageLogDirectory), o =>
                            {
                                string messageLine;
                                lock (LocalLogFileLock)
                                {
                                    using (
                                        FileStream fileStream = File.Open(o, FileMode.Open, FileAccess.Read,
                                            FileShare.Read))
                                    {
                                        using (StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8))
                                        {
                                            messageLine = streamReader.ReadToEnd();
                                        }
                                    }
                                }
                                if (string.IsNullOrEmpty(messageLine)) return;
                                Parallel.ForEach(
                                    messageLine.Split(new[] {Environment.NewLine}, StringSplitOptions.None),
                                    p =>
                                    {
                                        Match match = CORRADE_CONSTANTS.LocalMessageLogRegex.Match(p);
                                        if (!match.Success) return;
                                        DateTime messageDateTime;
                                        if (!DateTime.TryParse(match.Groups[1].Value, out messageDateTime)) return;
                                        string messageFirstName = match.Groups[2].Value;
                                        if (string.IsNullOrEmpty(messageFirstName)) return;
                                        string messageLastName = match.Groups[3].Value;
                                        if (string.IsNullOrEmpty(messageLastName)) return;
                                        ChatType messageType;
                                        if (!Enum.TryParse(match.Groups[4].Value, out messageType)) return;
                                        string message = match.Groups[5].Value;
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
                            switch (Reflection.GetEnumValueFromName<Action>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                        corradeCommandParameters.Message))
                                    .ToLowerInvariant()))
                            {
                                case Action.GET:
                                    // search by date
                                    DateTime getLocalMessageFromDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FROM)),
                                        corradeCommandParameters.Message), out getLocalMessageFromDate))
                                    {
                                        getLocalMessageFromDate = DateTime.MinValue;
                                    }
                                    DateTime getLocalMessageToDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TO)),
                                        corradeCommandParameters.Message), out getLocalMessageToDate))
                                    {
                                        getLocalMessageToDate = DateTime.MaxValue;
                                    }
                                    // build regular expressions based on fed data
                                    Regex getLocalMessageFirstNameRegex;
                                    try
                                    {
                                        getLocalMessageFirstNameRegex = new Regex(KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME)),
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
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME)),
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
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE)),
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
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.REGION)),
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
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                                            corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        getLocalMessageChatTypeRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    // cull the message list depending on what parameters have been specified
                                    localMessages.RemoveWhere(
                                        o =>
                                            (getLocalMessageToDate < o.DateTime &&
                                             getLocalMessageFromDate > o.DateTime) ||
                                            !getLocalMessageFirstNameRegex.IsMatch(o.FirstName) ||
                                            !getLocalMessageLastNameRegex.IsMatch(o.LastName) ||
                                            !getLocalMessageMessageRegex.IsMatch(o.Message) ||
                                            !getLocalMessageRegionNameRegex.IsMatch(o.RegionName) ||
                                            !getLocalMessageChatTypeRegex.IsMatch(Enum.GetName(typeof (ChatType),
                                                o.ChatType)));
                                    Parallel.ForEach(localMessages, o =>
                                    {
                                        lock (LockObject)
                                        {
                                            csv.AddRange(new[]
                                            {
                                                Reflection.GetNameFromEnumValue(ScriptKeys.TIME),
                                                o.DateTime.ToUniversalTime()
                                                    .ToString(Constants.LSL.DATE_TIME_STAMP)
                                            });
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.REGION), o.RegionName});
                                            csv.AddRange(new[]
                                            {
                                                Reflection.GetNameFromEnumValue(ScriptKeys.TYPE),
                                                Enum.GetName(typeof (ChatType),
                                                    o.ChatType)
                                            });
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME), o.FirstName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME), o.LastName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE), o.Message});
                                        }
                                    });
                                    break;
                                case Action.SEARCH:
                                    // build regular expressions based on fed data
                                    Regex searchLocalMessagesRegex;
                                    try
                                    {
                                        searchLocalMessagesRegex = new Regex(KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                            corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        throw new ScriptException(ScriptError.COULD_NOT_COMPILE_REGULAR_EXPRESSION);
                                    }
                                    Parallel.ForEach(localMessages, o =>
                                    {
                                        if (!searchLocalMessagesRegex.IsMatch(o.FirstName) &&
                                            !searchLocalMessagesRegex.IsMatch(o.LastName) &&
                                            !searchLocalMessagesRegex.IsMatch(o.Message) &&
                                            !searchLocalMessagesRegex.IsMatch(o.RegionName) &&
                                            !searchLocalMessagesRegex.IsMatch(Enum.GetName(typeof (ChatType), o.ChatType)))
                                            return;
                                        lock (LockObject)
                                        {
                                            csv.AddRange(new[]
                                            {
                                                Reflection.GetNameFromEnumValue(ScriptKeys.TIME),
                                                o.DateTime.ToUniversalTime()
                                                    .ToString(Constants.LSL.DATE_TIME_STAMP)
                                            });
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.REGION), o.RegionName});
                                            csv.AddRange(new[]
                                            {
                                                Reflection.GetNameFromEnumValue(ScriptKeys.TYPE),
                                                Enum.GetName(typeof (ChatType),
                                                    o.ChatType)
                                            });
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME), o.FirstName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME), o.LastName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE), o.Message});
                                        }
                                    });
                                    break;
                                default:
                                    throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                            }
                            break;
                        case Entity.REGION:
                            HashSet<RegionMessage> regionMessages = new HashSet<RegionMessage>();
                            Parallel.ForEach(Directory.GetFiles(corradeConfiguration.RegionMessageLogDirectory), o =>
                            {
                                string messageLine;
                                lock (RegionLogFileLock)
                                {
                                    using (
                                        FileStream fileStream = File.Open(o, FileMode.Open, FileAccess.Read,
                                            FileShare.Read))
                                    {
                                        using (StreamReader streamReader = new StreamReader(fileStream, Encoding.UTF8))
                                        {
                                            messageLine = streamReader.ReadToEnd();
                                        }
                                    }
                                }
                                if (string.IsNullOrEmpty(messageLine)) return;
                                Parallel.ForEach(
                                    messageLine.Split(new[] {Environment.NewLine}, StringSplitOptions.None),
                                    p =>
                                    {
                                        Match match = CORRADE_CONSTANTS.RegionMessageLogRegex.Match(p);
                                        if (!match.Success) return;
                                        DateTime messageDateTime;
                                        if (!DateTime.TryParse(match.Groups[1].Value, out messageDateTime)) return;
                                        string messageFirstName = match.Groups[2].Value;
                                        if (string.IsNullOrEmpty(messageFirstName)) return;
                                        string messageLastName = match.Groups[3].Value;
                                        if (string.IsNullOrEmpty(messageLastName)) return;
                                        string message = match.Groups[4].Value;
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
                            switch (Reflection.GetEnumValueFromName<Action>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                        corradeCommandParameters.Message))
                                    .ToLowerInvariant()))
                            {
                                case Action.GET:
                                    // search by date
                                    DateTime getRegionMessageFromDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FROM)),
                                        corradeCommandParameters.Message), out getRegionMessageFromDate))
                                    {
                                        getRegionMessageFromDate = DateTime.MinValue;
                                    }
                                    DateTime getRegionMessageToDate;
                                    if (!DateTime.TryParse(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TO)),
                                        corradeCommandParameters.Message), out getRegionMessageToDate))
                                    {
                                        getRegionMessageToDate = DateTime.MaxValue;
                                    }
                                    // build regular expressions based on fed data
                                    Regex getRegionMessageFirstNameRegex;
                                    try
                                    {
                                        getRegionMessageFirstNameRegex = new Regex(KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME)),
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
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME)),
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
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE)),
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
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.REGION)),
                                            corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        getRegionMessageRegionNameRegex = CORRADE_CONSTANTS.OneOrMoRegex;
                                    }
                                    // cull the message list depending on what parameters have been specified
                                    regionMessages.RemoveWhere(
                                        o =>
                                            (getRegionMessageToDate < o.DateTime &&
                                             getRegionMessageFromDate > o.DateTime) ||
                                            !getRegionMessageFirstNameRegex.IsMatch(o.FirstName) ||
                                            !getRegionMessageLastNameRegex.IsMatch(o.LastName) ||
                                            !getRegionMessageMessageRegex.IsMatch(o.Message) ||
                                            !getRegionMessageRegionNameRegex.IsMatch(o.RegionName));
                                    Parallel.ForEach(regionMessages, o =>
                                    {
                                        lock (LockObject)
                                        {
                                            csv.AddRange(new[]
                                            {
                                                Reflection.GetNameFromEnumValue(ScriptKeys.TIME),
                                                o.DateTime.ToUniversalTime()
                                                    .ToString(Constants.LSL.DATE_TIME_STAMP)
                                            });
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.REGION), o.RegionName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME), o.FirstName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME), o.LastName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE), o.Message});
                                        }
                                    });
                                    break;
                                case Action.SEARCH:
                                    // build regular expressions based on fed data
                                    Regex searchRegionMessagesRegex;
                                    try
                                    {
                                        searchRegionMessagesRegex = new Regex(KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                                            corradeCommandParameters.Message),
                                            RegexOptions.Compiled);
                                    }
                                    catch (Exception)
                                    {
                                        throw new ScriptException(ScriptError.COULD_NOT_COMPILE_REGULAR_EXPRESSION);
                                    }
                                    Parallel.ForEach(regionMessages, o =>
                                    {
                                        if (!searchRegionMessagesRegex.IsMatch(o.FirstName) &&
                                            !searchRegionMessagesRegex.IsMatch(o.LastName) &&
                                            !searchRegionMessagesRegex.IsMatch(o.Message) &&
                                            !searchRegionMessagesRegex.IsMatch(o.RegionName))
                                            return;
                                        lock (LockObject)
                                        {
                                            csv.AddRange(new[]
                                            {
                                                Reflection.GetNameFromEnumValue(ScriptKeys.TIME),
                                                o.DateTime.ToUniversalTime()
                                                    .ToString(Constants.LSL.DATE_TIME_STAMP)
                                            });
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.REGION), o.RegionName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME), o.FirstName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME), o.LastName});
                                            csv.AddRange(new[]
                                            {Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE), o.Message});
                                        }
                                    });
                                    break;
                                default:
                                    throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                    }
                    if (csv.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                    }
                };
        }
    }
}