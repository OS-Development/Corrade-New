///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> setprimitiveflags =
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
                    if (
                        !Services.FindPrimitive(Client,
                            Helpers.StringOrUUID(wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                                corradeCommandParameters.Message))),
                            range, corradeConfiguration.Range,
                            ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                    {
                        throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                    }
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                    {
                        throw new ScriptException(ScriptError.INVALID_POSITION);
                    }
                    bool physics;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.PHYSICS)),
                                    corradeCommandParameters.Message)),
                            out physics))
                    {
                        physics = !(primitive.Flags & PrimFlags.Physics).Equals(PrimFlags.None);
                    }
                    bool temporary;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TEMPORARY)),
                                    corradeCommandParameters.Message)),
                            out temporary))
                    {
                        temporary = !(primitive.Flags & PrimFlags.Temporary).Equals(PrimFlags.None);
                    }
                    bool phantom;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.PHANTOM)),
                                    corradeCommandParameters.Message)),
                            out phantom))
                    {
                        phantom = !(primitive.Flags & PrimFlags.Phantom).Equals(PrimFlags.None);
                    }
                    bool shadows;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.SHADOWS)),
                                    corradeCommandParameters.Message)),
                            out shadows))
                    {
                        shadows = !(primitive.Flags & PrimFlags.CastShadows).Equals(PrimFlags.None);
                    }
                    FieldInfo physicsShapeFieldInfo = typeof (PhysicsShapeType).GetFields(BindingFlags.Public |
                                                                                          BindingFlags.Static)
                        .AsParallel().FirstOrDefault(p => p.Name.Equals(wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                                corradeCommandParameters.Message)), StringComparison.Ordinal));
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
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DENSITY)),
                                    corradeCommandParameters.Message)),
                            out density))
                    {
                        density = primitive.PhysicsProps.Density;
                    }
                    float friction;
                    if (
                        !float.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FRICTION)),
                                    corradeCommandParameters.Message)),
                            out friction))
                    {
                        friction = primitive.PhysicsProps.Friction;
                    }
                    float restitution;
                    if (
                        !float.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.RESTITUTION)),
                                    corradeCommandParameters.Message)),
                            out restitution))
                    {
                        restitution = primitive.PhysicsProps.Restitution;
                    }
                    float gravity;
                    if (
                        !float.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.GRAVITY)),
                                    corradeCommandParameters.Message)),
                            out gravity))
                    {
                        gravity = primitive.PhysicsProps.GravityMultiplier;
                    }
                    Client.Objects.SetFlags(
                        Client.Network.Simulators.AsParallel()
                            .FirstOrDefault(o => o.Handle.Equals(primitive.RegionHandle)),
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