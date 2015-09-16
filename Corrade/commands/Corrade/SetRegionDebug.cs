///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> setregiondebug =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    if (!Client.Network.CurrentSim.IsEstateManager)
                    {
                        throw new ScriptException(ScriptError.NO_LAND_RIGHTS);
                    }
                    bool scripts;
                    if (
                        !bool.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.SCRIPTS)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant(), out scripts))
                    {
                        scripts = false;
                    }
                    bool collisions;
                    if (
                        !bool.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.COLLISIONS)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant(), out collisions))
                    {
                        collisions = false;
                    }
                    bool physics;
                    if (
                        !bool.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.PHYSICS)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant(), out physics))
                    {
                        physics = false;
                    }
                    Client.Estate.SetRegionDebug(!scripts, !collisions, !physics);
                };
        }
    }
}