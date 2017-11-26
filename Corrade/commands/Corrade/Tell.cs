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
using System.Reflection;
using System.Text;
using Corrade.Constants;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Timers;
using wasSharpNET.Diagnostics;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> tell =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int) Configuration.Permissions.Talk))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var data = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MESSAGE)),
                            corradeCommandParameters.Message));
                    var myName =
                        new List<string>(
                            wasOpenMetaverse.Helpers.GetAvatarNames(string.Join(" ", Client.Self.FirstName,
                                Client.Self.LastName)));
                    UUID sessionUUID;
                    var currentGroups = Enumerable.Empty<UUID>();
                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Entity>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                    corradeCommandParameters.Message))))
                    {
                        case Enumerations.Entity.CONFERENCE:
                            // check message length for SecondLife grids
                            if (string.IsNullOrEmpty(data) || wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                                Encoding.UTF8.GetByteCount(data) >
                                wasOpenMetaverse.Constants.CHAT.MAXIMUM_MESSAGE_LENGTH)
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TOO_MANY_OR_TOO_FEW_CHARACTERS_IN_MESSAGE);
                            // Get the session UUID
                            if (!UUID.TryParse(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SESSION)),
                                    corradeCommandParameters.Message)), out sessionUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_SESSION_SPECIFIED);

                            try
                            {
                                Locks.ClientInstanceSelfLock.EnterWriteLock();
                                if (!Client.Self.GroupChatSessions.ContainsKey(sessionUUID))
                                    Client.Self.ChatterBoxAcceptInvite(sessionUUID);
                                Client.Self.InstantMessageGroup(sessionUUID, data);
                            }
                            catch (Exception ex)
                            {
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), ex.Message);
                                throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_SEND_MESSAGE);
                            }
                            finally
                            {
                                Locks.ClientInstanceSelfLock.ExitWriteLock();
                            }
                            break;

                        case Enumerations.Entity.AVATAR:
                            UUID agentUUID;
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                            corradeCommandParameters.Message)), out agentUUID) &&
                                !Resolvers.AgentNameToUUID(Client,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(
                                                Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME)),
                                            corradeCommandParameters.Message)),
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                            corradeCommandParameters.Message)),
                                    corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout,
                                    new DecayingAlarm(corradeConfiguration.DataDecayType),
                                    ref agentUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                            // get instant message dialog type
                            var instantMessageDialogInfo = typeof(InstantMessageDialog).GetFields(
                                    BindingFlags.Public |
                                    BindingFlags.Static)
                                .AsParallel().FirstOrDefault(
                                    o =>
                                        o.Name.Equals(
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(
                                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.DIALOG)),
                                                    corradeCommandParameters.Message)),
                                            StringComparison.Ordinal));
                            var instantMessageDialog = instantMessageDialogInfo != null
                                ? (InstantMessageDialog)
                                instantMessageDialogInfo
                                    .GetValue(null)
                                : InstantMessageDialog.MessageFromAgent;
                            // check message length for SecondLife grids
                            if (wasOpenMetaverse.Helpers.IsSecondLife(Client))
                                switch (instantMessageDialog)
                                {
                                    case InstantMessageDialog.MessageFromAgent:
                                    case InstantMessageDialog.BusyAutoResponse:
                                        if (string.IsNullOrEmpty(data) ||
                                            Encoding.UTF8.GetByteCount(data) >
                                            wasOpenMetaverse.Constants.CHAT.MAXIMUM_MESSAGE_LENGTH)
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.TOO_MANY_OR_TOO_FEW_CHARACTERS_IN_MESSAGE);
                                        break;
                                }
                            // get whether the message is online of offline (defaults to offline)
                            var instantMessageOnlineInfo = typeof(InstantMessageOnline).GetFields(
                                    BindingFlags.Public |
                                    BindingFlags.Static)
                                .AsParallel().FirstOrDefault(
                                    o =>
                                        o.Name.Equals(
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(
                                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.ONLINE)),
                                                    corradeCommandParameters.Message)),
                                            StringComparison.Ordinal));
                            var instantMessageOnline = instantMessageOnlineInfo != null
                                ? (InstantMessageOnline)
                                instantMessageOnlineInfo
                                    .GetValue(null)
                                : InstantMessageOnline.Offline;
                            // get the session UUID (defaults to UUID.Zero)
                            if (!UUID.TryParse(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SESSION)),
                                    corradeCommandParameters.Message)), out sessionUUID))
                                sessionUUID = UUID.Zero;
                            // send the message
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            //Client.Self.InstantMessage(agentUUID, data);
                            Client.Self.InstantMessage(Client.Self.FirstName + @" " + Client.Self.LastName,
                                agentUUID, data, sessionUUID, instantMessageDialog,
                                instantMessageOnline, Client.Self.SimPosition,
                                Client.Network.CurrentSim.RegionID, new byte[] { });
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            // do not log empty messages
                            if (string.IsNullOrEmpty(data))
                                break;
                            // Log instant messages,
                            if (corradeConfiguration.InstantMessageLogEnabled)
                            {
                                var agentName = string.Empty;
                                if (!Resolvers.AgentUUIDToName(Client,
                                    agentUUID,
                                    corradeConfiguration.ServicesTimeout,
                                    ref agentName))
                                    throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);

                                var fullName =
                                    new List<string>(
                                        wasOpenMetaverse.Helpers.GetAvatarNames(agentName));

                                if (!fullName.Any())
                                    throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);

                                CorradeThreadPool[Threading.Enumerations.ThreadType.LOG].SpawnSequential(() =>
                                {
                                    try
                                    {
                                        lock (InstantMessageLogFileLock)
                                        {
                                            Directory.CreateDirectory(corradeConfiguration.InstantMessageLogDirectory);

                                            var path =
                                                $"{Path.Combine(corradeConfiguration.InstantMessageLogDirectory, $"{fullName.First()} {fullName.Last()}")}.{CORRADE_CONSTANTS.LOG_FILE_EXTENSION}";

                                            using (var fileStream = new FileStream(path,
                                                FileMode.Append, FileAccess.Write, FileShare.None, 16384, true))
                                            {
                                                using (
                                                    var logWriter = new StreamWriter(fileStream, Encoding.UTF8)
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
                                                Enumerations.ConsoleMessage
                                                    .COULD_NOT_WRITE_TO_INSTANT_MESSAGE_LOG_FILE),
                                            ex.PrettyPrint());
                                    }
                                }, corradeConfiguration.MaximumLogThreads, corradeConfiguration.ServicesTimeout);
                            }
                            break;

                        case Enumerations.Entity.GROUP:
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
                                    groupUUID = new UUID(corradeCommandParameters.Group.UUID);
                                    break;
                            }
                            if (
                                !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                                    ref currentGroups))
                                throw new Command.ScriptException(Enumerations.ScriptError
                                    .COULD_NOT_GET_CURRENT_GROUPS);
                            if (!new HashSet<UUID>(currentGroups).Contains(groupUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.NOT_IN_GROUP);
                            if (string.IsNullOrEmpty(data) || wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                                Encoding.UTF8.GetByteCount(data) >
                                wasOpenMetaverse.Constants.CHAT.MAXIMUM_MESSAGE_LENGTH)
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TOO_MANY_OR_TOO_FEW_CHARACTERS_IN_MESSAGE);
                            Locks.ClientInstanceSelfLock.EnterReadLock();
                            var gotChatSession =
                                Client.Self.GroupChatSessions.ContainsKey(groupUUID);
                            Locks.ClientInstanceSelfLock.ExitReadLock();
                            if (!gotChatSession)
                            {
                                if (
                                    !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                        groupUUID,
                                        GroupPowers.JoinChat,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        new DecayingAlarm(corradeConfiguration.DataDecayType)))
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);

                                if (
                                    !Services.JoinGroupChat(Client, groupUUID,
                                        corradeConfiguration.ServicesTimeout))
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.UNABLE_TO_JOIN_GROUP_CHAT);
                            }
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.InstantMessageGroup(groupUUID, data);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        case Enumerations.Entity.LOCAL:
                            int chatChannel;
                            if (
                                !int.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.CHANNEL)),
                                            corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture,
                                    out chatChannel))
                                chatChannel = 0;
                            // Add support for sending messages on negative channels.
                            switch (chatChannel < 0)
                            {
                                case false:
                                    var chatTypeInfo = typeof(ChatType).GetFields(BindingFlags.Public |
                                                                                  BindingFlags.Static)
                                        .AsParallel().FirstOrDefault(
                                            o =>
                                                o.Name.Equals(
                                                    wasInput(
                                                        KeyValue.Get(
                                                            wasOutput(
                                                                Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                                                    .TYPE)),
                                                            corradeCommandParameters.Message)),
                                                    StringComparison.Ordinal));
                                    var chatType = chatTypeInfo != null
                                        ? (ChatType)
                                        chatTypeInfo
                                            .GetValue(null)
                                        : ChatType.Normal;
                                    // check for message length depending on the type of message
                                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client))
                                        switch (chatType)
                                        {
                                            case ChatType.Normal:
                                            case ChatType.Debug:
                                            case ChatType.OwnerSay:
                                            case ChatType.RegionSay:
                                            case ChatType.RegionSayTo:
                                            case ChatType.Shout:
                                            case ChatType.Whisper:
                                                if (string.IsNullOrEmpty(data) || Encoding.UTF8.GetByteCount(data) >
                                                    wasOpenMetaverse.Constants.CHAT.MAXIMUM_MESSAGE_LENGTH)
                                                    throw new Command.ScriptException(
                                                        Enumerations.ScriptError
                                                            .TOO_MANY_OR_TOO_FEW_CHARACTERS_IN_MESSAGE);
                                                break;
                                        }

                                    // RLV - Broadcast chat redirects if RLV is enabled and there are set @redirchat rules.
                                    if (corradeConfiguration.EnableRLV)
                                        switch (chatType)
                                        {
                                            case ChatType.Normal:
                                            case ChatType.Shout:
                                            case ChatType.Whisper:
                                                var succeeded = false;
                                                RLVRules
                                                    .AsParallel()
                                                    .Where(o => string.Equals(o.Behaviour,
                                                        Reflection.GetNameFromEnumValue(RLV.RLVBehaviour.REDIRCHAT)))
                                                    .ForAll(o =>
                                                    {
                                                        var notifyOptions = o.Option.Split(';');
                                                        if (notifyOptions.Length.Equals(0))
                                                            return;

                                                        int channel;
                                                        if (!int.TryParse(notifyOptions[0], NumberStyles.Integer,
                                                                Utils.EnUsCulture, out channel) ||
                                                            channel < 1)
                                                            return;

                                                        Locks.ClientInstanceSelfLock.EnterWriteLock();
                                                        Client.Self.Chat(data, channel, chatType);
                                                        Locks.ClientInstanceSelfLock.ExitWriteLock();

                                                        succeeded = true;
                                                    });

                                                if (succeeded)
                                                    return;
                                                break;
                                        }
                                    // send the message
                                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                                    Client.Self.Chat(data, chatChannel, chatType);
                                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                                    break;

                                default:
                                    // that's how the big boys do it
                                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                                    Client.Self.ReplyToScriptDialog(chatChannel, 0, data, Client.Self.AgentID);
                                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                                    break;
                            }
                            break;

                        case Enumerations.Entity.ESTATE:
                            if (!Client.Network.CurrentSim.IsEstateManager)
                                throw new Command.ScriptException(Enumerations.ScriptError
                                    .NO_ESTATE_POWERS_FOR_COMMAND);

                            Locks.ClientInstanceEstateLock.EnterWriteLock();
                            Client.Estate.EstateMessage(data);
                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                            break;

                        case Enumerations.Entity.REGION:
                            if (!Client.Network.CurrentSim.IsEstateManager)
                                throw new Command.ScriptException(Enumerations.ScriptError
                                    .NO_ESTATE_POWERS_FOR_COMMAND);

                            Locks.ClientInstanceEstateLock.EnterWriteLock();
                            Client.Estate.SimulatorMessage(data);
                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }
                };
        }
    }
}