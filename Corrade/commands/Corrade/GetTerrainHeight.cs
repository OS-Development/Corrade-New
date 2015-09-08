///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> getterrainheight =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string region =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REGION)),
                            message));
                    Simulator simulator =
                        Client.Network.Simulators.FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                    StringComparison.OrdinalIgnoreCase));
                    if (simulator == null)
                    {
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    }
                    Vector3 southwest;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.SOUTHWEST)),
                                    message)),
                            out southwest))
                    {
                        southwest = new Vector3(0, 0, 0);
                    }
                    Vector3 northeast;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.NORTHEAST)),
                                    message)),
                            out northeast))
                    {
                        northeast = new Vector3(255, 255, 0);
                    }

                    int x1 = Convert.ToInt32(southwest.X);
                    int y1 = Convert.ToInt32(southwest.Y);
                    int x2 = Convert.ToInt32(northeast.X);
                    int y2 = Convert.ToInt32(northeast.Y);

                    if (x1 > x2)
                    {
                        wasXORSwap(ref x1, ref x2);
                    }
                    if (y1 > y2)
                    {
                        wasXORSwap(ref y1, ref y2);
                    }

                    int sx = x2 - x1 + 1;
                    int sy = y2 - y1 + 1;

                    float[] csv = new float[sx*sy];
                    Parallel.ForEach(Enumerable.Range(x1, sx), x => Parallel.ForEach(Enumerable.Range(y1, sy), y =>
                    {
                        float height;
                        csv[sx*x + y] = simulator.TerrainHeightAtPoint(x, y, out height)
                            ? height
                            : -1;
                    }));
                    if (csv.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(csv.Select(o => o.ToString(CultureInfo.DefaultThreadCurrentCulture))));
                    }
                };
        }
    }
}