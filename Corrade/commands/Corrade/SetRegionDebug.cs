///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> setregiondebug =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                        (int) Configuration.Permissions.Land))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    if (!Client.Network.CurrentSim.IsEstateManager)
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_LAND_RIGHTS);
                    bool scripts;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SCRIPTS)),
                                    corradeCommandParameters.Message))
                            , out scripts))
                        scripts = !Client.Network.CurrentSim.Flags.HasFlag(RegionFlags.SkipScripts);
                    bool collisions;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.COLLISIONS)),
                                    corradeCommandParameters.Message))
                            , out collisions))
                        collisions = !Client.Network.CurrentSim.Flags.HasFlag(RegionFlags.SkipCollisions);
                    bool physics;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PHYSICS)),
                                    corradeCommandParameters.Message))
                            , out physics))
                        physics = !Client.Network.CurrentSim.Flags.HasFlag(RegionFlags.SkipPhysics);
                    Locks.ClientInstanceEstateLock.EnterWriteLock();
                    Client.Estate.SetRegionDebug(!scripts, !collisions, !physics);
                    Locks.ClientInstanceEstateLock.ExitWriteLock();
                };
        }
    }
}