///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> grab =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)),
                            out range))
                    {
                        range = corradeConfiguration.Range;
                    }
                    Primitive primitive = null;
                    if (
                        !FindPrimitive(
                            StringOrUUID(wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)),
                                corradeCommandParameters.Message))),
                            range,
                            ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                    {
                        throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                    }
                    Vector3 uvCoord;
                    if (!Vector3.TryParse(wasInput(wasKeyValueGet(
                        wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TEXTURE)), corradeCommandParameters.Message)),
                        out uvCoord))
                    {
                        throw new ScriptException(ScriptError.INVALID_TEXTURE_COORDINATES);
                    }
                    Vector3 stCoord;
                    if (!Vector3.TryParse(wasInput(wasKeyValueGet(
                        wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.SURFACE)), corradeCommandParameters.Message)),
                        out stCoord))
                    {
                        throw new ScriptException(ScriptError.INVALID_SURFACE_COORDINATES);
                    }
                    int faceIndex;
                    if (!int.TryParse(wasInput(wasKeyValueGet(
                        wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.FACE)), corradeCommandParameters.Message)),
                        out faceIndex))
                    {
                        throw new ScriptException(ScriptError.INVALID_FACE_SPECIFIED);
                    }
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION)),
                                corradeCommandParameters.Message)), out position))
                    {
                        throw new ScriptException(ScriptError.INVALID_POSITION);
                    }
                    Vector3 normal;
                    if (
                        !Vector3.TryParse(
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.NORMAL)),
                                corradeCommandParameters.Message)), out normal))
                    {
                        throw new ScriptException(ScriptError.INVALID_NORMAL_VECTOR);
                    }
                    Vector3 binormal;
                    if (
                        !Vector3.TryParse(
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.BINORMAL)),
                                corradeCommandParameters.Message)), out binormal))
                    {
                        throw new ScriptException(ScriptError.INVALID_BINORMAL_VECTOR);
                    }
                    Client.Objects.ClickObject(
                        Client.Network.Simulators.AsParallel()
                            .FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle)),
                        primitive.LocalID, uvCoord, stCoord, faceIndex, position,
                        normal, binormal);
                };
        }
    }
}