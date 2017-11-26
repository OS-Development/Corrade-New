///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                replytoteleportlure =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Movement))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                        var agentUUID = UUID.Zero;
                        var sessionUUID = UUID.Zero;
                        var action = Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))
                        );
                        switch (action)
                        {
                            case Enumerations.Action.ACCEPT:
                            case Enumerations.Action.DECLINE:
                                if (
                                    !UUID.TryParse(
                                        wasInput(KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                            corradeCommandParameters.Message)),
                                        out agentUUID) && !Resolvers.AgentNameToUUID(Client,
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME)),
                                                corradeCommandParameters.Message)),
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(
                                                    Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                                corradeCommandParameters.Message)),
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        new DecayingAlarm(corradeConfiguration.DataDecayType),
                                        ref agentUUID))
                                    throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                                goto case Enumerations.Action.IGNORE;
                            case Enumerations.Action.IGNORE:
                                if (
                                    !UUID.TryParse(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SESSION)),
                                                corradeCommandParameters.Message)),
                                        out sessionUUID))
                                    throw new Command.ScriptException(Enumerations.ScriptError.NO_SESSION_SPECIFIED);
                                TeleportLure teleportLure;
                                lock (TeleportLuresLock)
                                {
                                    if (!TeleportLures.TryGetValue(sessionUUID, out teleportLure))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.TELEPORT_LURE_NOT_FOUND);
                                }
                                break;
                        }

                        switch (action)
                        {
                            case Enumerations.Action.ACCEPT:
                                // remove teleport lure
                                lock (TeleportLuresLock)
                                {
                                    TeleportLures.Remove(sessionUUID);
                                }
                                Locks.ClientInstanceSelfLock.EnterWriteLock();
                                Client.Self.TeleportLureRespond(agentUUID, sessionUUID, true);
                                Locks.ClientInstanceSelfLock.ExitWriteLock();
                                break;

                            case Enumerations.Action.DECLINE:
                                // remove teleport lure
                                lock (TeleportLuresLock)
                                {
                                    TeleportLures.Remove(sessionUUID);
                                }
                                Locks.ClientInstanceSelfLock.EnterWriteLock();
                                Client.Self.TeleportLureRespond(agentUUID, sessionUUID, false);
                                Locks.ClientInstanceSelfLock.ExitWriteLock();
                                break;

                            case Enumerations.Action.PURGE:
                                lock (TeleportLuresLock)
                                {
                                    TeleportLures.Clear();
                                }
                                break;

                            case Enumerations.Action.IGNORE:
                                lock (TeleportLuresLock)
                                {
                                    TeleportLures.Remove(sessionUUID);
                                }
                                break;

                            default:
                                throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                        }
                    };
        }
    }
}