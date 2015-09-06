using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> setprimitiveflags =
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
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION)),
                                    message)),
                            out position))
                    {
                        throw new ScriptException(ScriptError.INVALID_POSITION);
                    }
                    bool physics;
                    if (
                        !bool.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.PHYSICS)),
                                    message)),
                            out physics))
                    {
                        physics = !(primitive.Flags & PrimFlags.Physics).Equals(0);
                    }
                    bool temporary;
                    if (
                        !bool.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TEMPORARY)),
                                    message)),
                            out temporary))
                    {
                        temporary = !(primitive.Flags & PrimFlags.Temporary).Equals(0);
                    }
                    bool phantom;
                    if (
                        !bool.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.PHANTOM)),
                                    message)),
                            out phantom))
                    {
                        phantom = !(primitive.Flags & PrimFlags.Phantom).Equals(0);
                    }
                    bool shadows;
                    if (
                        !bool.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.SHADOWS)),
                                    message)),
                            out shadows))
                    {
                        shadows = !(primitive.Flags & PrimFlags.CastShadows).Equals(0);
                    }
                    FieldInfo physicsShapeFieldInfo = typeof (PhysicsShapeType).GetFields(BindingFlags.Public |
                                                                                          BindingFlags.Static)
                        .AsParallel().FirstOrDefault(p => p.Name.Equals(wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)),
                                message)), StringComparison.Ordinal));
                    PhysicsShapeType physicsShapeType;
                    switch (physicsShapeFieldInfo != null)
                    {
                        case true:
                            physicsShapeType = (PhysicsShapeType) physicsShapeFieldInfo.GetValue(null);
                            break;
                        default:
                            physicsShapeType = primitive.PhysicsProps.PhysicsShapeType;
                            break;
                    }
                    float density;
                    if (
                        !float.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DENSITY)),
                                    message)),
                            out density))
                    {
                        density = primitive.PhysicsProps.Density;
                    }
                    float friction;
                    if (
                        !float.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.FRICTION)),
                                    message)),
                            out friction))
                    {
                        friction = primitive.PhysicsProps.Friction;
                    }
                    float restitution;
                    if (
                        !float.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.RESTITUTION)),
                                    message)),
                            out restitution))
                    {
                        restitution = primitive.PhysicsProps.Restitution;
                    }
                    float gravity;
                    if (
                        !float.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.GRAVITY)),
                                    message)),
                            out gravity))
                    {
                        gravity = primitive.PhysicsProps.GravityMultiplier;
                    }
                    Client.Objects.SetFlags(
                        Client.Network.Simulators.FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle)),
                        primitive.LocalID,
                        physics,
                        temporary,
                        phantom,
                        shadows,
                        physicsShapeType, density,
                        friction, restitution,
                        gravity);
                };
        }
    }
}