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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                setregionterrainvariables
                    =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Land))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        if (!Client.Network.CurrentSim.IsEstateManager)
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_LAND_RIGHTS);
                        float waterHeight;
                        if (
                            !float.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.WATERHEIGHT)),
                                        corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                out waterHeight))
                            waterHeight = wasOpenMetaverse.Constants.REGION.DEFAULT_WATER_HEIGHT;
                        float terrainRaiseLimit;
                        if (
                            !float.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                            .TERRAINRAISELIMIT)),
                                        corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                out terrainRaiseLimit))
                            terrainRaiseLimit = wasOpenMetaverse.Constants.REGION.DEFAULT_TERRAIN_RAISE_LIMIT;
                        float terrainLowerLimit;
                        if (
                            !float.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                            .TERRAINLOWERLIMIT)),
                                        corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                out terrainLowerLimit))
                            terrainLowerLimit = wasOpenMetaverse.Constants.REGION.DEFAULT_TERRAIN_LOWER_LIMIT;
                        bool useEstateSun;
                        if (
                            !bool.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.USEESTATESUN)),
                                        corradeCommandParameters.Message)), out useEstateSun))
                            useEstateSun = wasOpenMetaverse.Constants.REGION.DEFAULT_USE_ESTATE_SUN;
                        bool fixedSun;
                        if (
                            !bool.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIXEDSUN)),
                                        corradeCommandParameters.Message)), out fixedSun))
                            fixedSun = wasOpenMetaverse.Constants.REGION.DEFAULT_FIXED_SUN;
                        float sunPosition;
                        if (
                            !float.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SUNPOSITION)),
                                        corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                out sunPosition))
                            sunPosition = wasOpenMetaverse.Constants.REGION.SUNRISE;
                        Locks.ClientInstanceEstateLock.EnterWriteLock();
                        Client.Estate.SetTerrainVariables(waterHeight, terrainRaiseLimit, terrainLowerLimit,
                            useEstateSun,
                            fixedSun, sunPosition);
                        Locks.ClientInstanceEstateLock.ExitWriteLock();
                    };
        }
    }
}