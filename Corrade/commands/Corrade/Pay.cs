///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
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
            public static Action<Command.CorradeCommandParameters, Dictionary<string, string>> pay =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Economy))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    int amount;
                    if (
                        !int.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AMOUNT)),
                                    corradeCommandParameters.Message)),
                            out amount))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_AMOUNT);
                    }
                    if (amount.Equals(0))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_AMOUNT);
                    }
                    if (!Services.UpdateBalance(Client, corradeConfiguration.ServicesTimeout))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_OBTAIN_MONEY_BALANCE);
                    }
                    lock (Locks.ClientInstanceSelfLock)
                    {
                        if (Client.Self.Balance < amount)
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.INSUFFICIENT_FUNDS);
                        }
                    }
                    UUID targetUUID;
                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Entity>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Enumerations.Entity.GROUP:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.GiveGroupMoney(corradeCommandParameters.Group.UUID, amount,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                            corradeCommandParameters.Message)));
                            }
                            break;
                        case Enumerations.Entity.AVATAR:
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                            corradeCommandParameters.Message)), out targetUUID) &&
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
                                    new Time.DecayingAlarm(corradeConfiguration.DataDecayType),
                                    ref targetUUID))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                            }
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.GiveAvatarMoney(targetUUID, amount,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                            corradeCommandParameters.Message)));
                            }
                            break;
                        case Enumerations.Entity.OBJECT:
                            if (
                                !UUID.TryParse(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                                            corradeCommandParameters.Message)),
                                    out targetUUID))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_PAY_TARGET);
                            }
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.GiveObjectMoney(targetUUID, amount,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                                            corradeCommandParameters.Message)));
                            }
                            break;
                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }
                };
        }
    }
}