///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Structures;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
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
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        }
                        UUID agentUUID;
                        if (
                            !UUID.TryParse(
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                    corradeCommandParameters.Message)),
                                out agentUUID) && !Resolvers.AgentNameToUUID(Client,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME)),
                                            corradeCommandParameters.Message)),
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                            corradeCommandParameters.Message)),
                                    corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                    new Time.DecayingAlarm(corradeConfiguration.DataDecayType),
                                    ref agentUUID))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                        }
                        UUID sessionUUID;
                        if (
                            !UUID.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SESSION)),
                                        corradeCommandParameters.Message)),
                                out sessionUUID))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_SESSION_SPECIFIED);
                        }
                        TeleportLure teleportLure;
                        lock (TeleportLureLock)
                        {
                            teleportLure = TeleportLures.AsParallel().FirstOrDefault(o => o.Session.Equals(sessionUUID));
                        }
                        if (teleportLure.Equals(default(TeleportLure)))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.TELEPORT_LURE_NOT_FOUND);
                        }
                        // remove teleport lure
                        lock (TeleportLureLock)
                        {
                            TeleportLures.Remove(teleportLure);
                        }
                        Client.Self.TeleportLureRespond(agentUUID, sessionUUID,
                            Reflection.GetEnumValueFromName<Enumerations.Action>(
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                        corradeCommandParameters.Message))
                                    .ToLowerInvariant()).Equals(Enumerations.Action.ACCEPT));
                    };
        }
    }
}