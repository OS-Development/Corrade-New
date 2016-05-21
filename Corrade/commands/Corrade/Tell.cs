///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Helpers = wasOpenMetaverse.Helpers;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> tell =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Talk))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string data = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE)),
                            corradeCommandParameters.Message));
                    List<string> myName =
                        new List<string>(
                            Helpers.GetAvatarNames(string.Join(" ", Client.Self.FirstName, Client.Self.LastName)));
                    switch (
                        Reflection.GetEnumValueFromName<Entity>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Entity.AVATAR:
                            UUID agentUUID;
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT)),
                                            corradeCommandParameters.Message)), out agentUUID) &&
                                !Resolvers.AgentNameToUUID(Client,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(
                                                Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME)),
                                            corradeCommandParameters.Message)),
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME)),
                                            corradeCommandParameters.Message)),
                                    corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout,
                                    new Time.DecayingAlarm(corradeConfiguration.DataDecayType),
                                    ref agentUUID))
                            {
                                throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                            }
                            if (string.IsNullOrEmpty(data) ||
                                (Helpers.IsSecondLife(Client) &&
                                 Encoding.UTF8.GetByteCount(data) >
                                 Constants.CHAT.MAXIMUM_MESSAGE_LENGTH))
                            {
                                throw new ScriptException(ScriptError.TOO_MANY_OR_TOO_FEW_CHARACTERS_IN_MESSAGE);
                            }
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.InstantMessage(agentUUID, data);
                            }
                            // Log instant messages,
                            if (corradeConfiguration.InstantMessageLogEnabled)
                            {
                                string agentName = string.Empty;
                                if (!Resolvers.AgentUUIDToName(Client,
                                    agentUUID,
                                    corradeConfiguration.ServicesTimeout,
                                    ref agentName))
                                {
                                    throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                                }
                                List<string> fullName =
                                    new List<string>(
                                        Helpers.GetAvatarNames(agentName));
                                CorradeThreadPool[CorradeThreadType.LOG].SpawnSequential(() =>
                                {
                                    try
                                    {
                                        lock (InstantMessageLogFileLock)
                                        {
                                            using (FileStream fileStream = File.Open(Path.Combine(
                                                corradeConfiguration.InstantMessageLogDirectory,
                                                string.Join(" ", fullName.First(), fullName.Last())) + "." +
                                                                                     CORRADE_CONSTANTS
                                                                                         .LOG_FILE_EXTENSION,
                                                FileMode.Append, FileAccess.Write, FileShare.None))
                                            {
                                                using (
                                                    StreamWriter logWriter = new StreamWriter(fileStream, Encoding.UTF8)
                                                    )
                                                {
                                                    logWriter.WriteLine("[{0}] {1} {2} : {3}",
                                                        DateTime.Now.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                                            Utils.EnUsCulture.DateTimeFormat),
                                                        myName.First(),
                                                        myName.Last(),
                                                        data);
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // or fail and append the fail message.
                                        Feedback(
                                            Reflection.GetNameFromEnumValue(
                                                ConsoleError.COULD_NOT_WRITE_TO_INSTANT_MESSAGE_LOG_FILE),
                                            ex.Message);
                                    }
                                }, corradeConfiguration.MaximumLogThreads, corradeConfiguration.ServicesTimeout);
                            }
                            break;
                        case Entity.GROUP:
                            UUID groupUUID;
                            string target = wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TARGET)),
                                    corradeCommandParameters.Message));
                            switch (string.IsNullOrEmpty(target))
                            {
                                case false:
                                    if (!UUID.TryParse(target, out groupUUID) &&
                                        !Resolvers.GroupNameToUUID(Client, target, corradeConfiguration.ServicesTimeout,
                                            corradeConfiguration.DataTimeout,
                                            new Time.DecayingAlarm(corradeConfiguration.DataDecayType), ref groupUUID))
                                        throw new ScriptException(ScriptError.GROUP_NOT_FOUND);
                                    break;
                                default:
                                    groupUUID = corradeCommandParameters.Group.UUID;
                                    break;
                            }
                            IEnumerable<UUID> currentGroups = Enumerable.Empty<UUID>();
                            if (
                                !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                                    ref currentGroups))
                            {
                                throw new ScriptException(ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                            }
                            if (!new HashSet<UUID>(currentGroups).Contains(groupUUID))
                            {
                                throw new ScriptException(ScriptError.NOT_IN_GROUP);
                            }
                            if (string.IsNullOrEmpty(data) || (Helpers.IsSecondLife(Client) &&
                                                               Encoding.UTF8.GetByteCount(data) >
                                                               Constants.CHAT.MAXIMUM_MESSAGE_LENGTH))
                            {
                                throw new ScriptException(ScriptError.TOO_MANY_OR_TOO_FEW_CHARACTERS_IN_MESSAGE);
                            }
                            bool gotChatSession;
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                gotChatSession =
                                    Client.Self.GroupChatSessions.ContainsKey(groupUUID);
                            }
                            if (!gotChatSession)
                            {
                                if (
                                    !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                        groupUUID,
                                        GroupPowers.JoinChat,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                                {
                                    throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                                }

                                if (
                                    !Services.JoinGroupChat(Client, groupUUID,
                                        corradeConfiguration.ServicesTimeout))
                                {
                                    throw new ScriptException(ScriptError.UNABLE_TO_JOIN_GROUP_CHAT);
                                }
                            }
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.InstantMessageGroup(groupUUID, data);
                            }
                            corradeConfiguration.Groups.AsParallel().Where(
                                o => o.UUID.Equals(groupUUID) && o.ChatLogEnabled).ForAll(
                                    o =>
                                    {
                                        CorradeThreadPool[CorradeThreadType.LOG].SpawnSequential(() =>
                                        {
                                            // Attempt to write to log file,
                                            try
                                            {
                                                lock (GroupLogFileLock)
                                                {
                                                    using (FileStream fileStream = File.Open(o.ChatLog,
                                                        FileMode.Append, FileAccess.Write, FileShare.None))
                                                    {
                                                        using (
                                                            StreamWriter logWriter = new StreamWriter(fileStream,
                                                                Encoding.UTF8)
                                                            )
                                                        {
                                                            logWriter.WriteLine("[{0}] {1} {2} : {3}",
                                                                DateTime.Now.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                                                    Utils.EnUsCulture.DateTimeFormat),
                                                                myName.First(),
                                                                myName.Last(),
                                                                data);
                                                        }
                                                    }
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                // or fail and append the fail message.
                                                Feedback(
                                                    Reflection.GetNameFromEnumValue(
                                                        ConsoleError.COULD_NOT_WRITE_TO_GROUP_CHAT_LOG_FILE),
                                                    ex.Message);
                                            }
                                        }, corradeConfiguration.MaximumLogThreads, corradeConfiguration.ServicesTimeout);
                                    });
                            break;
                        case Entity.LOCAL:
                            if (string.IsNullOrEmpty(data) || (Helpers.IsSecondLife(Client) &&
                                                               Encoding.UTF8.GetByteCount(data) >
                                                               Constants.CHAT.MAXIMUM_MESSAGE_LENGTH))
                            {
                                throw new ScriptException(ScriptError.TOO_MANY_OR_TOO_FEW_CHARACTERS_IN_MESSAGE);
                            }
                            int chatChannel;
                            if (
                                !int.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.CHANNEL)),
                                            corradeCommandParameters.Message)),
                                    out chatChannel))
                            {
                                chatChannel = 0;
                            }
                            FieldInfo chatTypeInfo = typeof (ChatType).GetFields(BindingFlags.Public |
                                                                                 BindingFlags.Static)
                                .AsParallel().FirstOrDefault(
                                    o =>
                                        o.Name.Equals(
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                                                    corradeCommandParameters.Message)),
                                            StringComparison.Ordinal));
                            ChatType chatType = chatTypeInfo != null
                                ? (ChatType)
                                    chatTypeInfo
                                        .GetValue(null)
                                : ChatType.Normal;
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.Chat(
                                    data,
                                    chatChannel,
                                    chatType);
                            }
                            // Log local chat,
                            if (corradeConfiguration.LocalMessageLogEnabled)
                            {
                                List<string> fullName =
                                    new List<string>(
                                        Helpers.GetAvatarNames(string.Join(" ", Client.Self.FirstName,
                                            Client.Self.LastName)));
                                CorradeThreadPool[CorradeThreadType.LOG].SpawnSequential(() =>
                                {
                                    try
                                    {
                                        lock (LocalLogFileLock)
                                        {
                                            using (FileStream fileStream = File.Open(Path.Combine(
                                                corradeConfiguration.LocalMessageLogDirectory,
                                                Client.Network.CurrentSim.Name) + "." +
                                                                                     CORRADE_CONSTANTS
                                                                                         .LOG_FILE_EXTENSION,
                                                FileMode.Append, FileAccess.Write, FileShare.None))
                                            {
                                                using (
                                                    StreamWriter logWriter = new StreamWriter(fileStream,
                                                        Encoding.UTF8)
                                                    )
                                                {
                                                    logWriter.WriteLine("[{0}] {1} {2} ({3}) : {4}",
                                                        DateTime.Now.ToString(CORRADE_CONSTANTS.DATE_TIME_STAMP,
                                                            Utils.EnUsCulture.DateTimeFormat),
                                                        fullName.First(),
                                                        fullName.Last(), Enum.GetName(typeof (ChatType), chatType),
                                                        data);
                                                    //logWriter.Flush();
                                                    //logWriter.Close();
                                                }
                                            }
                                        }
                                    }
                                    catch (Exception ex)
                                    {
                                        // or fail and append the fail message.
                                        Feedback(
                                            Reflection.GetNameFromEnumValue(
                                                ConsoleError.COULD_NOT_WRITE_TO_LOCAL_MESSAGE_LOG_FILE),
                                            ex.Message);
                                    }
                                }, corradeConfiguration.MaximumLogThreads, corradeConfiguration.ServicesTimeout);
                            }
                            break;
                        case Entity.ESTATE:
                            lock (Locks.ClientInstanceEstateLock)
                            {
                                Client.Estate.EstateMessage(data);
                            }
                            break;
                        case Entity.REGION:
                            lock (Locks.ClientInstanceEstateLock)
                            {
                                Client.Estate.SimulatorMessage(data);
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                    }
                };
        }
    }
}