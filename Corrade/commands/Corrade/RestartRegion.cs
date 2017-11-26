///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using CorradeConfigurationSharp;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> restartregion =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int) Configuration.Permissions.Land))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    if (!Client.Network.CurrentSim.IsEstateManager)
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_LAND_RIGHTS);
                    uint delay;
                    if (
                        !uint.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DELAY)),
                                corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture, out delay))
                        delay = wasOpenMetaverse.Constants.ESTATE.REGION_RESTART_DELAY;
                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))))
                    {
                        case Enumerations.Action.RESTART:
                            // Manually override Client.Estate.RestartRegion();
                            Locks.ClientInstanceEstateLock.EnterWriteLock();
                            Client.Estate.EstateOwnerMessage(
                                wasOpenMetaverse.Constants.ESTATE.MESSAGES.REGION_RESTART_MESSAGE,
                                delay.ToString(Utils.EnUsCulture));
                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                            break;

                        case Enumerations.Action.CANCEL:
                            Locks.ClientInstanceEstateLock.EnterWriteLock();
                            Client.Estate.CancelRestart();
                            Locks.ClientInstanceEstateLock.ExitWriteLock();
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_RESTART_ACTION);
                    }
                };
        }
    }
}