///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> pay = (commandGroup, message, result) =>
            {
                if (
                    !HasCorradePermission(commandGroup.Name, (int) Permissions.Economy))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                int amount;
                if (
                    !int.TryParse(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.AMOUNT)), message)),
                        out amount))
                {
                    throw new ScriptException(ScriptError.INVALID_PAY_AMOUNT);
                }
                if (amount.Equals(0))
                {
                    throw new ScriptException(ScriptError.INVALID_PAY_AMOUNT);
                }
                if (!UpdateBalance(corradeConfiguration.ServicesTimeout))
                {
                    throw new ScriptException(ScriptError.UNABLE_TO_OBTAIN_MONEY_BALANCE);
                }
                if (Client.Self.Balance < amount)
                {
                    throw new ScriptException(ScriptError.INSUFFICIENT_FUNDS);
                }
                UUID targetUUID;
                switch (
                    wasGetEnumValueFromDescription<Entity>(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ENTITY)),
                                message)).ToLowerInvariant()))
                {
                    case Entity.GROUP:
                        Client.Self.GiveGroupMoney(commandGroup.UUID, amount,
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REASON)),
                                    message)));
                        break;
                    case Entity.AVATAR:
                        if (
                            !UUID.TryParse(
                                wasInput(
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT)),
                                        message)), out targetUUID) && !AgentNameToUUID(
                                            wasInput(
                                                wasKeyValueGet(
                                                    wasOutput(
                                                        wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME)),
                                                    message)),
                                            wasInput(
                                                wasKeyValueGet(
                                                    wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME)),
                                                    message)),
                                            corradeConfiguration.ServicesTimeout,
                                            corradeConfiguration.DataTimeout,
                                            ref targetUUID))
                        {
                            throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                        }
                        Client.Self.GiveAvatarMoney(targetUUID, amount,
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REASON)),
                                    message)));
                        break;
                    case Entity.OBJECT:
                        if (
                            !UUID.TryParse(
                                wasInput(
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TARGET)),
                                        message)),
                                out targetUUID))
                        {
                            throw new ScriptException(ScriptError.INVALID_PAY_TARGET);
                        }
                        Client.Self.GiveObjectMoney(targetUUID, amount,
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REASON)),
                                    message)));
                        break;
                    default:
                        throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                }
            };
        }
    }
}