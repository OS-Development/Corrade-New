///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> restartregion =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Configuration.Permissions.Land))
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
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DELAY)),
                                corradeCommandParameters.Message))
                                .ToLowerInvariant(), out delay))
                    {
                        delay = LINDEN_CONSTANTS.ESTATE.REGION_RESTART_DELAY;
                    }
                    switch (
                        Reflection.GetEnumValueFromName<Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Action.RESTART:
                            // Manually override Client.Estate.RestartRegion();
                            Client.Estate.EstateOwnerMessage(
                                LINDEN_CONSTANTS.ESTATE.MESSAGES.REGION_RESTART_MESSAGE,
                                delay.ToString(Utils.EnUsCulture));
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