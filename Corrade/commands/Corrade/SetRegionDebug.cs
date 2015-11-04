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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> setregiondebug =
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
                    bool scripts;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.SCRIPTS)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant(), out scripts))
                    {
                        scripts = !Client.Network.CurrentSim.Flags.HasFlag(RegionFlags.SkipScripts);
                    }
                    bool collisions;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.COLLISIONS)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant(), out collisions))
                    {
                        collisions = !Client.Network.CurrentSim.Flags.HasFlag(RegionFlags.SkipCollisions);
                    }
                    bool physics;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.PHYSICS)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant(), out physics))
                    {
                        physics = !Client.Network.CurrentSim.Flags.HasFlag(RegionFlags.SkipPhysics);
                    }
                    Client.Estate.SetRegionDebug(!scripts, !collisions, !physics);
                };
        }
    }
}