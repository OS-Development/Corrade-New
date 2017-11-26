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
                setprimitivetexturedata =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Interact))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        float range;
                        if (
                            !float.TryParse(
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                    corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                out range))
                            range = corradeConfiguration.Range;
                        Primitive primitive = null;
                        var item = wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                            corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(item))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
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
                                    throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                                break;

                            default:
                                if (
                                    !Services.FindPrimitive(Client,
                                        item,
                                        range,
                                        ref primitive,
                                        corradeConfiguration.DataTimeout))
                                    throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                                break;
                        }
                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                        var simulator = Client.Network.Simulators.AsParallel()
                            .FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle));
                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                        if (simulator == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                        var face =
                            wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FACE)),
                                corradeCommandParameters.Message));
                        uint i;
                        switch (!uint.TryParse(face, NumberStyles.Integer, Utils.EnUsCulture, out i))
                        {
                            case true:
                                switch (face.ToLowerInvariant())
                                {
                                    case "all":
                                        Enumerable.Range(0, primitive.Textures.FaceTextures.Length).AsParallel()
                                            .Select(o => (uint) o).ForAll(o =>
                                            {
                                                if (primitive.Textures.FaceTextures[o] == null)
                                                    primitive.Textures.FaceTextures[o] =
                                                        primitive.Textures.CreateFace(o);
                                                primitive.Textures.FaceTextures[o] =
                                                    primitive.Textures.FaceTextures[o].wasCSVToStructure(wasInput(
                                                        KeyValue.Get(
                                                            wasOutput(
                                                                Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                                                    .DATA)),
                                                            corradeCommandParameters.Message)), wasInput);
                                            });
                                        break;

                                    case "default":
                                        primitive.Textures.DefaultTexture =
                                            primitive.Textures.DefaultTexture.wasCSVToStructure(wasInput(
                                                KeyValue.Get(
                                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                                    corradeCommandParameters.Message)), wasInput);
                                        break;

                                    default:
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.INVALID_FACE_SPECIFIED);
                                }
                                break;

                            default:
                                if (i > Primitive.TextureEntry.MAX_FACES)
                                    throw new Command.ScriptException(Enumerations.ScriptError.INVALID_FACE_SPECIFIED);
                                if (primitive.Textures.FaceTextures[i] == null)
                                    primitive.Textures.FaceTextures[i] = primitive.Textures.CreateFace(i);
                                primitive.Textures.FaceTextures[i] =
                                    primitive.Textures.FaceTextures[i].wasCSVToStructure(wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                            corradeCommandParameters.Message)), wasInput);
                                break;
                        }
                        Locks.ClientInstanceObjectsLock.EnterWriteLock();
                        Client.Objects.SetTextures(simulator,
                            primitive.LocalID, primitive.Textures);
                        Locks.ClientInstanceObjectsLock.ExitWriteLock();
                    };
        }
    }
}