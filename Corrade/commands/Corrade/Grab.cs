///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;

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
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)),
                            out range))
                    {
                        range = corradeConfiguration.Range;
                    }
                    Primitive primitive = null;
                    var item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                    {
                        throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
                    }
                    UUID itemUUID;
                    switch (UUID.TryParse(item, out itemUUID))
                    {
                        case true:
                            if (
                                !Services.FindPrimitive(Client,
                                    itemUUID,
                                    range,
                                    ref primitive,
                                    corradeConfiguration.DataTimeout))
                            {
                                throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                            }
                            break;
                        default:
                            if (
                                !Services.FindPrimitive(Client,
                                    item,
                                    range,
                                    ref primitive,
                                    corradeConfiguration.DataTimeout))
                            {
                                throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                            }
                            break;
                    }
                    Vector3 uvCoord;
                    if (!Vector3.TryParse(wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TEXTURE)),
                        corradeCommandParameters.Message)),
                        out uvCoord))
                    {
                        throw new ScriptException(ScriptError.INVALID_TEXTURE_COORDINATES);
                    }
                    Vector3 stCoord;
                    if (!Vector3.TryParse(wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.SURFACE)),
                        corradeCommandParameters.Message)),
                        out stCoord))
                    {
                        throw new ScriptException(ScriptError.INVALID_SURFACE_COORDINATES);
                    }
                    int faceIndex;
                    if (!int.TryParse(wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FACE)), corradeCommandParameters.Message)),
                        out faceIndex))
                    {
                        throw new ScriptException(ScriptError.INVALID_FACE_SPECIFIED);
                    }
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)), out position))
                    {
                        throw new ScriptException(ScriptError.INVALID_POSITION);
                    }
                    Vector3 normal;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.NORMAL)),
                                    corradeCommandParameters.Message)), out normal))
                    {
                        throw new ScriptException(ScriptError.INVALID_NORMAL_VECTOR);
                    }
                    Vector3 binormal;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.BINORMAL)),
                                    corradeCommandParameters.Message)), out binormal))
                    {
                        throw new ScriptException(ScriptError.INVALID_BINORMAL_VECTOR);
                    }
                    Simulator simulator;
                    lock (Locks.ClientInstanceNetworkLock)
                    {
                        simulator = Client.Network.Simulators.AsParallel()
                            .FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle));
                    }
                    if (simulator == null)
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    lock (Locks.ClientInstanceObjectsLock)
                    {
                        Client.Objects.ClickObject(
                            simulator,
                            primitive.LocalID, uvCoord, stCoord, faceIndex, position,
                            normal, binormal);
                    }
                };
        }
    }
}