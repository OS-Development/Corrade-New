using System;
using System.Collections.Generic;
using System.Globalization;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> restartregion =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    if (!Client.Network.CurrentSim.IsEstateManager)
                    {
                        throw new ScriptException(ScriptError.NO_LAND_RIGHTS);
                    }
                    uint delay;
                    if (
                        !uint.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DELAY)), message))
                                .ToLowerInvariant(), out delay))
                    {
                        delay = LINDEN_CONSTANTS.ESTATE.REGION_RESTART_DELAY;
                    }
                    switch (
                        wasGetEnumValueFromDescription<Action>(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)),
                                    message)).ToLowerInvariant()))
                    {
                        case Action.RESTART:
                            // Manually override Client.Estate.RestartRegion();
                            Client.Estate.EstateOwnerMessage(
                                LINDEN_CONSTANTS.ESTATE.MESSAGES.REGION_RESTART_MESSAGE,
                                delay.ToString(CultureInfo.DefaultThreadCurrentCulture));
                            break;
                        case Action.CANCEL:
                            Client.Estate.CancelRestart();
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_RESTART_ACTION);
                    }
                };
        }
    }
}