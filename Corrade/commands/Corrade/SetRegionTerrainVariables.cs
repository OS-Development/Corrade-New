///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using CorradeConfiguration;
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> setregionterrainvariables =
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
                    float waterHeight;
                    if (
                        !float.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.WATERHEIGHT)),
                                    corradeCommandParameters.Message)), out waterHeight))
                    {
                        waterHeight = Constants.REGION.DEFAULT_WATER_HEIGHT;
                    }
                    float terrainRaiseLimit;
                    if (
                        !float.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TERRAINRAISELIMIT)),
                                    corradeCommandParameters.Message)), out terrainRaiseLimit))
                    {
                        terrainRaiseLimit = Constants.REGION.DEFAULT_TERRAIN_RAISE_LIMIT;
                    }
                    float terrainLowerLimit;
                    if (
                        !float.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TERRAINLOWERLIMIT)),
                                    corradeCommandParameters.Message)), out terrainLowerLimit))
                    {
                        terrainLowerLimit = Constants.REGION.DEFAULT_TERRAIN_LOWER_LIMIT;
                    }
                    bool useEstateSun;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.USEESTATESUN)),
                                    corradeCommandParameters.Message)), out useEstateSun))
                    {
                        useEstateSun = Constants.REGION.DEFAULT_USE_ESTATE_SUN;
                    }
                    bool fixedSun;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FIXEDSUN)),
                                    corradeCommandParameters.Message)), out fixedSun))
                    {
                        fixedSun = Constants.REGION.DEFAULT_FIXED_SUN;
                    }
                    float sunPosition;
                    if (
                        !float.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.SUNPOSITION)),
                                    corradeCommandParameters.Message)), out sunPosition))
                    {
                        sunPosition = Constants.REGION.SUNRISE;
                    }
                    Client.Estate.SetTerrainVariables(waterHeight, terrainRaiseLimit, terrainLowerLimit, useEstateSun,
                        fixedSun, sunPosition);
                };
        }
    }
}