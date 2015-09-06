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
            public static Action<Group, string, Dictionary<string, string>> setprimitivetexturedata =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.RANGE)), message)),
                            out range))
                    {
                        range = corradeConfiguration.Range;
                    }
                    Primitive primitive = null;
                    if (
                        !FindPrimitive(
                            StringOrUUID(wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)), message))),
                            range,
                            ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                    {
                        throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                    }
                    string face =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.FACE)),
                            message));
                    int i;
                    switch (!int.TryParse(face, out i))
                    {
                        case true:
                            switch (face.ToLowerInvariant())
                            {
                                case "all":
                                    i = primitive.Textures.FaceTextures.Count() - 1;
                                    do
                                    {
                                        if (primitive.Textures.FaceTextures[i] == null)
                                        {
                                            primitive.Textures.FaceTextures[i] =
                                                primitive.Textures.CreateFace((uint) i);
                                        }
                                        wasCSVToStructure(
                                            wasInput(
                                                wasKeyValueGet(
                                                    wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DATA)),
                                                    message)),
                                            ref primitive.Textures.FaceTextures[i]);
                                    } while (--i > -1);
                                    break;
                                case "default":
                                    wasCSVToStructure(
                                        wasInput(
                                            wasKeyValueGet(
                                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DATA)),
                                                message)),
                                        ref primitive.Textures.DefaultTexture);
                                    break;
                                default:
                                    throw new ScriptException(ScriptError.INVALID_FACE_SPECIFIED);
                            }
                            break;
                        default:
                            if (i < 0 || i > Primitive.TextureEntry.MAX_FACES)
                                throw new ScriptException(ScriptError.INVALID_FACE_SPECIFIED);
                            if (primitive.Textures.FaceTextures[i] == null)
                            {
                                primitive.Textures.FaceTextures[i] = primitive.Textures.CreateFace((uint) i);
                            }
                            wasCSVToStructure(
                                wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DATA)),
                                    message)),
                                ref primitive.Textures.FaceTextures[i]);
                            break;
                    }
                    Client.Objects.SetTextures(
                        Client.Network.Simulators.FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle)),
                        primitive.LocalID, primitive.Textures);
                };
        }
    }
}