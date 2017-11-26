///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getregionterrainheights =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Land))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        var region =
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                    corradeCommandParameters.Message));
                        Simulator simulator;
                        switch (!string.IsNullOrEmpty(region))
                        {
                            case true:
                                Locks.ClientInstanceNetworkLock.EnterReadLock();
                                simulator = Client.Network.Simulators.AsParallel().FirstOrDefault(
                                    o =>
                                        o.Name.Equals(
                                            string.IsNullOrEmpty(region)
                                                ? Client.Network.CurrentSim.Name
                                                : region,
                                            StringComparison.OrdinalIgnoreCase));
                                Locks.ClientInstanceNetworkLock.ExitReadLock();
                                if (simulator == null)
                                    throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                                break;

                            default:
                                simulator = Client.Network.CurrentSim;
                                break;
                        }
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(new[]
                            {
                                simulator.TerrainStartHeight00, // Low SW
                                simulator.TerrainHeightRange00, // High SW
                                simulator.TerrainStartHeight01, // Low NW
                                simulator.TerrainHeightRange01, // High NW
                                simulator.TerrainStartHeight10, // Low SE
                                simulator.TerrainHeightRange10, // High SE
                                simulator.TerrainStartHeight11, // Low NE
                                simulator.TerrainHeightRange11 // High NE
                            }.Select(o => o.ToString(Utils.EnUsCulture))));
                    };
        }
    }
}