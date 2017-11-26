///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Corrade.Structures;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> conference =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Talk))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var sessionUUID = UUID.Zero;
                    var LockObject = new object();
                    var csv = new List<string>();
                    switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                    ))
                    {
                        case Enumerations.Action.CREATE: //starts a new conference
                            var conferenceParticipants = new HashSet<UUID>();
                            CSV.ToEnumerable(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AVATARS)),
                                            corradeCommandParameters.Message)))
                                .AsParallel()
                                .Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                                {
                                    UUID agentUUID;
                                    if (!UUID.TryParse(o, out agentUUID))
                                    {
                                        var fullName = new List<string>(wasOpenMetaverse.Helpers.GetAvatarNames(o));
                                        if (!fullName.Any() ||
                                            !Resolvers.AgentNameToUUID(Client, fullName.First(), fullName.Last(),
                                                corradeConfiguration.ServicesTimeout,
                                                corradeConfiguration.DataTimeout,
                                                new DecayingAlarm(corradeConfiguration.DataDecayType),
                                                ref agentUUID))
                                            return;
                                    }
                                    lock (LockObject)
                                    {
                                        conferenceParticipants.Add(agentUUID);
                                    }
                                });

                            if (!conferenceParticipants.Any())
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_AVATARS_FOUND);

                            var tmpSessionUUID = UUID.Random();
                            var conferenceName = string.Empty;
                            var succeeded = false;
                            var conferenceStartedEvent = new ManualResetEventSlim(false);
                            EventHandler<GroupChatJoinedEventArgs> GroupChatJoinedEventHandler = (sender, args) =>
                            {
                                if (!args.TmpSessionID.Equals(tmpSessionUUID)) return;
                                sessionUUID = args.SessionID;
                                conferenceName = args.SessionName;
                                succeeded = args.Success;
                                conferenceStartedEvent.Set();
                            };
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.GroupChatJoined += GroupChatJoinedEventHandler;
                            Client.Self.StartIMConference(conferenceParticipants.ToList(), tmpSessionUUID);
                            if (!conferenceStartedEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                            {
                                Client.Self.GroupChatJoined -= GroupChatJoinedEventHandler;
                                Locks.ClientInstanceSelfLock.ExitWriteLock();
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_STARTING_CONFERENCE);
                            }
                            Client.Self.GroupChatJoined -= GroupChatJoinedEventHandler;
                            Locks.ClientInstanceSelfLock.ExitWriteLock();

                            if (!succeeded)
                                throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_START_CONFERENCE);

                            lock (ConferencesLock)
                            {
                                Conferences.Add(new Conference
                                {
                                    Name = conferenceName,
                                    Session = sessionUUID,
                                    Restored = false
                                });
                            }
                            // Save the conference state.
                            SaveConferenceState.Invoke();
                            // Return the conference details.
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(new[]
                                {
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)), conferenceName,
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SESSION)),
                                    sessionUUID.ToString()
                                }));
                            break;

                        case Enumerations.Action.DETAIL: // lists the avatar names and UUIDs of participants
                            // Get the session UUID
                            if (!UUID.TryParse(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SESSION)),
                                    corradeCommandParameters.Message)), out sessionUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_SESSION_SPECIFIED);
                            // Join the chat if not yet joined
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            if (!Client.Self.GroupChatSessions.ContainsKey(sessionUUID))
                                Client.Self.ChatterBoxAcceptInvite(sessionUUID);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();

                            if (Services.JoinGroupChat(Client, sessionUUID, corradeConfiguration.ServicesTimeout))
                                throw new Command.ScriptException(Enumerations.ScriptError.SESSION_NOT_FOUND);

                            List<ChatSessionMember> members;
                            if (!Client.Self.GroupChatSessions.TryGetValue(sessionUUID, out members))
                                throw new Command.ScriptException(Enumerations.ScriptError.SESSION_NOT_FOUND);
                            members.AsParallel().ForAll(o =>
                            {
                                var agentName = string.Empty;
                                if (Resolvers.AgentUUIDToName(Client, o.AvatarKey, corradeConfiguration.ServicesTimeout,
                                    ref agentName))
                                    lock (LockObject)
                                    {
                                        csv.Add(agentName);
                                        csv.Add(o.AvatarKey.ToString());
                                    }
                            });
                            if (csv.Any())
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                    CSV.FromEnumerable(csv));
                            break;

                        case Enumerations.Action.LIST: // lists the known conferences that we are part of
                            lock (ConferencesLock)
                            {
                                Conferences.AsParallel().ForAll(o =>
                                {
                                    lock (LockObject)
                                    {
                                        csv.Add(o.Name);
                                        csv.Add(o.Session.ToString());
                                        csv.Add(o.Restored.ToString());
                                    }
                                });
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