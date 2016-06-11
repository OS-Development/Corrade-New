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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> setobjectmediadata =
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
                                !Services.FindObject(Client,
                                    itemUUID,
                                    range,
                                    ref primitive,
                                    corradeConfiguration.DataTimeout))
                            {
                                throw new ScriptException(ScriptError.OBJECT_NOT_FOUND);
                            }
                            break;
                        default:
                            if (
                                !Services.FindObject(Client,
                                    item,
                                    range,
                                    ref primitive,
                                    corradeConfiguration.DataTimeout))
                            {
                                throw new ScriptException(ScriptError.OBJECT_NOT_FOUND);
                            }
                            break;
                    }
                    Simulator simulator;
                    lock (Locks.ClientInstanceNetworkLock)
                    {
                        simulator = Client.Network.Simulators.AsParallel()
                            .FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle));
                    }
                    if (simulator == null)
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    uint face;
                    if (
                        !uint.TryParse(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FACE)),
                                    corradeCommandParameters.Message)), out face))
                        throw new ScriptException(ScriptError.INVALID_FACE_SPECIFIED);
                    MediaEntry[] faceMediaEntries = null;
                    lock (Locks.ClientInstanceObjectsLock)
                    {
                        Client.Objects.RequestObjectMedia(primitive.ID, simulator,
                            (succeeded, version, faceMedia) =>
                            {
                                switch (succeeded)
                                {
                                    case true:
                                        if (face >= faceMedia.Length)
                                            throw new ScriptException(ScriptError.INVALID_FACE_SPECIFIED);
                                        faceMediaEntries = faceMedia;
                                        break;
                                    default:
                                        throw new ScriptException(ScriptError.COULD_NOT_RETRIEVE_OBJECT_MEDIA);
                                }
                            });
                    }
                    wasCSVToStructure(
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                            corradeCommandParameters.Message)),
                        ref faceMediaEntries[face]);
                    lock (Locks.ClientInstanceObjectsLock)
                    {
                        Client.Objects.UpdateObjectMedia(primitive.ID, faceMediaEntries, simulator);
                    }
                };
        }
    }
}