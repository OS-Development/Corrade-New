///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using CorradeConfigurationSharp;
using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Linq;
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
                getregionterraintextures
                    =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int)Configuration.Permissions.Land))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        }
                        var region =
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                    corradeCommandParameters.Message));
                        Simulator simulator;
                        switch (!string.IsNullOrEmpty(region))
                        {
                            case true:
                                lock (Locks.ClientInstanceNetworkLock)
                                {
                                    simulator =
                                        Client.Network.Simulators.AsParallel().FirstOrDefault(
                                            o =>
                                                o.Name.Equals(
                                                    string.IsNullOrEmpty(region)
                                                        ? Client.Network.CurrentSim.Name
                                                        : region,
                                                    StringComparison.OrdinalIgnoreCase));
                                }
                                if (simulator == null)
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                                }
                                break;

                            default:
                                simulator = Client.Network.CurrentSim;
                                break;
                        }
                        List<UUID> data;
                        lock (Locks.ClientInstanceNetworkLock)
                        {
                            data = new List<UUID>
                            {
                                simulator.TerrainDetail0,
                                simulator.TerrainDetail1,
                                simulator.TerrainDetail2,
                                simulator.TerrainDetail3
                            };
                        }
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(data.Select(o => o.ToString())));
                    };
        }
    }
}
