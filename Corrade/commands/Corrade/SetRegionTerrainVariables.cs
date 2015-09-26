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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> setregionterrainvariables =
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
                    float waterHeight;
                    if (
                        !float.TryParse(
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.WATERHEIGHT)),
                                corradeCommandParameters.Message)), out waterHeight))
                    {
                        waterHeight = LINDEN_CONSTANTS.REGION.DEFAULT_WATER_HEIGHT;
                    }
                    float terrainRaiseLimit;
                    if (
                        !float.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TERRAINRAISELIMIT)),
                                    corradeCommandParameters.Message)), out terrainRaiseLimit))
                    {
                        terrainRaiseLimit = LINDEN_CONSTANTS.REGION.DEFAULT_TERRAIN_RAISE_LIMIT;
                    }
                    float terrainLowerLimit;
                    if (
                        !float.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TERRAINLOWERLIMIT)),
                                    corradeCommandParameters.Message)), out terrainLowerLimit))
                    {
                        terrainLowerLimit = LINDEN_CONSTANTS.REGION.DEFAULT_TERRAIN_LOWER_LIMIT;
                    }
                    bool useEstateSun;
                    if (
                        !bool.TryParse(
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.USEESTATESUN)),
                                corradeCommandParameters.Message)), out useEstateSun))
                    {
                        useEstateSun = LINDEN_CONSTANTS.REGION.DEFAULT_USE_ESTATE_SUN;
                    }
                    bool fixedSun;
                    if (
                        !bool.TryParse(
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.FIXEDSUN)),
                                corradeCommandParameters.Message)), out fixedSun))
                    {
                        fixedSun = LINDEN_CONSTANTS.REGION.DEFAULT_FIXED_SUN;
                    }
                    float sunPosition;
                    if (
                        !float.TryParse(
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.SUNPOSITION)),
                                corradeCommandParameters.Message)), out sunPosition))
                    {
                        sunPosition = LINDEN_CONSTANTS.REGION.SUNRISE;
                    }
                    Client.Estate.SetTerrainVariables(waterHeight, terrainRaiseLimit, terrainLowerLimit, useEstateSun,
                        fixedSun, sunPosition);
                };
        }
    }
}