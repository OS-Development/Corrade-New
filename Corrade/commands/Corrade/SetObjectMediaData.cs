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
using Helpers = wasOpenMetaverse.Helpers;

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
                    string item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                    {
                        throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
                    }
                    UUID itemUUID;
                    if (UUID.TryParse(item, out itemUUID))
                    {
                        if (
                            !Services.FindPrimitive(Client,
                                itemUUID,
                                range,
                                corradeConfiguration.Range,
                                ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                        {
                            throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                        }
                    }
                    else
                    {
                        if (
                            !Services.FindPrimitive(Client,
                                item,
                                range,
                                corradeConfiguration.Range,
                                ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                        {
                            throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                        }
                    }
                    // if the primitive is not an object (the root) or the primitive
                    // is not an object as an avatar attachment then bail out
                    if (!primitive.ParentID.Equals(0) && !primitive.ParentID.Equals(Client.Self.LocalID))
                    {
                        throw new ScriptException(ScriptError.ITEM_IS_NOT_AN_OBJECT);
                    }
                    uint face;
                    if (
                        !uint.TryParse(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FACE)),
                                    corradeCommandParameters.Message)), out face))
                        throw new ScriptException(ScriptError.INVALID_FACE_SPECIFIED);
                    MediaEntry[] faceMediaEntries = null;
                    Client.Objects.RequestObjectMedia(primitive.ID,
                        Client.Network.Simulators.AsParallel()
                            .FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle)),
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
                    wasCSVToStructure(
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                            corradeCommandParameters.Message)),
                        ref faceMediaEntries[face]);
                    Client.Objects.UpdateObjectMedia(primitive.ID, faceMediaEntries,
                        Client.Network.Simulators.AsParallel()
                            .FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle)));
                };
        }
    }
}