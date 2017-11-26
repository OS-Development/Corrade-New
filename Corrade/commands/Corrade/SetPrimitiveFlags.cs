///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
                setprimitiveflags =
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
                        Vector3 position;
                        if (
                            !Vector3.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                        corradeCommandParameters.Message)),
                                out position))
                            throw new Command.ScriptException(Enumerations.ScriptError.INVALID_POSITION);
                        bool physics;
                        if (
                            !bool.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PHYSICS)),
                                        corradeCommandParameters.Message)),
                                out physics))
                            physics = primitive.Flags.IsMaskFlagSet(PrimFlags.Physics);
                        bool temporary;
                        if (
                            !bool.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TEMPORARY)),
                                        corradeCommandParameters.Message)),
                                out temporary))
                            temporary = primitive.Flags.IsMaskFlagSet(PrimFlags.Temporary);
                        bool phantom;
                        if (
                            !bool.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PHANTOM)),
                                        corradeCommandParameters.Message)),
                                out phantom))
                            phantom = primitive.Flags.IsMaskFlagSet(PrimFlags.Phantom);
                        bool shadows;
                        if (
                            !bool.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SHADOWS)),
                                        corradeCommandParameters.Message)),
                                out shadows))
                            shadows = primitive.Flags.IsMaskFlagSet(PrimFlags.CastShadows);
                        var physicsShapeFieldInfo = typeof(PhysicsShapeType).GetFields(BindingFlags.Public |
                                                                                       BindingFlags.Static)
                            .AsParallel().FirstOrDefault(p => p.Name.Equals(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
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
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DENSITY)),
                                        corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                out density))
                            density = primitive.PhysicsProps.Density;
                        float friction;
                        if (
                            !float.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FRICTION)),
                                        corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                out friction))
                            friction = primitive.PhysicsProps.Friction;
                        float restitution;
                        if (
                            !float.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RESTITUTION)),
                                        corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                out restitution))
                            restitution = primitive.PhysicsProps.Restitution;
                        float gravity;
                        if (
                            !float.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.GRAVITY)),
                                        corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                out gravity))
                            gravity = primitive.PhysicsProps.GravityMultiplier;
                        Locks.ClientInstanceObjectsLock.EnterWriteLock();
                        Client.Objects.SetFlags(simulator,
                            primitive.LocalID,
                            physics,
                            temporary,
                            phantom,
                            shadows,
                            physicsShapeType, density,
                            friction, restitution,
                            gravity);
                        Locks.ClientInstanceObjectsLock.ExitWriteLock();
                    };
        }
    }
}