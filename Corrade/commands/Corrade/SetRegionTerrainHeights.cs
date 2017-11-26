///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
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
                setregionterrainheights =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Land))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        if (!Client.Network.CurrentSim.IsEstateManager)
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_LAND_RIGHTS);
                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                        var simHeights = new List<float>
                        {
                            Client.Network.CurrentSim.TerrainStartHeight00, // Low SW
                            Client.Network.CurrentSim.TerrainHeightRange00, // High SW
                            Client.Network.CurrentSim.TerrainStartHeight01, // Low NW
                            Client.Network.CurrentSim.TerrainHeightRange01, // High NW
                            Client.Network.CurrentSim.TerrainStartHeight10, // Low SE
                            Client.Network.CurrentSim.TerrainHeightRange10, // High SE
                            Client.Network.CurrentSim.TerrainStartHeight11, // Low NE
                            Client.Network.CurrentSim.TerrainHeightRange11 // High NE
                        };
                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                        var setHeights = new float[8];
                        var data = CSV.ToEnumerable(
                            wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                corradeCommandParameters.Message))).ToList();
                        Enumerable.Range(0, 8).AsParallel().ForAll(
                            o =>
                            {
                                float outFloat;
                                setHeights[o] = data.ElementAtOrDefault(o) != null &&
                                                float.TryParse(data[o], NumberStyles.Float, Utils.EnUsCulture,
                                                    out outFloat)
                                    ? outFloat
                                    : simHeights[o];
                            });
                        Locks.ClientInstanceEstateLock.EnterWriteLock();
                        Client.Estate.SetRegionTerrainHeights(
                            setHeights[0],
                            setHeights[1],
                            setHeights[2],
                            setHeights[3],
                            setHeights[4],
                            setHeights[5],
                            setHeights[6],
                            setHeights[7]
                        );
                        Locks.ClientInstanceEstateLock.ExitWriteLock();
                    };
        }
    }
}