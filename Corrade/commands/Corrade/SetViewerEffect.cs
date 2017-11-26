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
using Corrade.Structures.Effects;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Timers;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> setviewereffect
                =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    UUID effectUUID;
                    if (!UUID.TryParse(wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ID)),
                            corradeCommandParameters.Message)),
                        out effectUUID))
                        effectUUID = UUID.Random();
                    Vector3 offset;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(
                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.OFFSET)),
                                    corradeCommandParameters.Message)),
                            out offset))
                        offset = Vector3.Zero;
                    var viewerEffectType = Reflection.GetEnumValueFromName<Enumerations.ViewerEffectType>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.EFFECT)),
                                corradeCommandParameters.Message))
                    );
                    var targetUUID = UUID.Zero;
                    switch (viewerEffectType)
                    {
                        case Enumerations.ViewerEffectType.SPHERE:
                        case Enumerations.ViewerEffectType.BEAM:
                        case Enumerations.ViewerEffectType.POINT:
                        case Enumerations.ViewerEffectType.LOOK:
                            switch (viewerEffectType)
                            {
                                case Enumerations.ViewerEffectType.BEAM:
                                case Enumerations.ViewerEffectType.POINT:
                                case Enumerations.ViewerEffectType.LOOK:
                                    var item = wasInput(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                                        corradeCommandParameters.Message));
                                    switch (!string.IsNullOrEmpty(item))
                                    {
                                        case true:
                                            float range;
                                            if (
                                                !float.TryParse(
                                                    wasInput(KeyValue.Get(
                                                        wasOutput(
                                                            Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                                        corradeCommandParameters.Message)), NumberStyles.Float,
                                                    Utils.EnUsCulture,
                                                    out range))
                                                range = corradeConfiguration.Range;
                                            Primitive primitive = null;
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
                                                        throw new Command.ScriptException(
                                                            Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                                                    break;

                                                default:
                                                    if (
                                                        !Services.FindPrimitive(Client,
                                                            item,
                                                            range,
                                                            ref primitive,
                                                            corradeConfiguration.DataTimeout))
                                                        throw new Command.ScriptException(
                                                            Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                                                    break;
                                            }
                                            targetUUID = primitive.ID;
                                            break;

                                        default:
                                            if (
                                                !UUID.TryParse(
                                                    wasInput(KeyValue.Get(
                                                        wasOutput(
                                                            Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                                        corradeCommandParameters.Message)),
                                                    out targetUUID) && !Resolvers.AgentNameToUUID(Client,
                                                    wasInput(
                                                        KeyValue.Get(
                                                            wasOutput(
                                                                Reflection.GetNameFromEnumValue(
                                                                    Command.ScriptKeys.FIRSTNAME)),
                                                            corradeCommandParameters.Message)),
                                                    wasInput(
                                                        KeyValue.Get(
                                                            wasOutput(
                                                                Reflection.GetNameFromEnumValue(
                                                                    Command.ScriptKeys.LASTNAME)),
                                                            corradeCommandParameters.Message)),
                                                    corradeConfiguration.ServicesTimeout,
                                                    corradeConfiguration.DataTimeout,
                                                    new DecayingAlarm(corradeConfiguration.DataDecayType),
                                                    ref targetUUID))
                                                throw new Command.ScriptException(
                                                    Enumerations.ScriptError.AGENT_NOT_FOUND);
                                            break;
                                    }
                                    break;
                            }
                            switch (viewerEffectType)
                            {
                                case Enumerations.ViewerEffectType.LOOK:
                                    var lookAtTypeInfo = typeof(LookAtType).GetFields(BindingFlags.Public |
                                                                                      BindingFlags.Static)
                                        .AsParallel().FirstOrDefault(
                                            o =>
                                                o.Name.Equals(
                                                    wasInput(
                                                        KeyValue.Get(
                                                            wasOutput(
                                                                Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                                                    .TYPE)),
                                                            corradeCommandParameters.Message)),
                                                    StringComparison.Ordinal));
                                    var lookAtType = (LookAtType?) lookAtTypeInfo?.GetValue(null) ??
                                                     LookAtType.None;
                                    // Check whether the specified UUID belongs to a different effect type.
                                    lock (PointAtEffectsLock)
                                    {
                                        if (PointAtEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.EFFECT_UUID_BELONGS_TO_DIFFERENT_EFFECT);
                                    }
                                    lock (BeamEffectsLock)
                                    {
                                        if (BeamEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.EFFECT_UUID_BELONGS_TO_DIFFERENT_EFFECT);
                                    }
                                    lock (SphereEffectsLock)
                                    {
                                        if (SphereEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.EFFECT_UUID_BELONGS_TO_DIFFERENT_EFFECT);
                                    }
                                    // Trigger the effect.
                                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                                    Client.Self.LookAtEffect(Client.Self.AgentID, targetUUID, offset,
                                        lookAtType, effectUUID);
                                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                                    // Update the list of effects.
                                    lock (LookAtEffectsLock)
                                    {
                                        if (LookAtEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                            LookAtEffects.RemoveWhere(o => o.Effect.Equals(effectUUID));
                                        if (!lookAtType.Equals(LookAtType.None))
                                            LookAtEffects.Add(new LookAtEffect
                                            {
                                                Effect = effectUUID,
                                                Offset = offset,
                                                Source = Client.Self.AgentID,
                                                Target = targetUUID,
                                                Type = lookAtType
                                            });
                                    }
                                    break;

                                case Enumerations.ViewerEffectType.POINT:
                                    var pointAtTypeInfo = typeof(PointAtType).GetFields(BindingFlags.Public |
                                                                                        BindingFlags.Static)
                                        .AsParallel().FirstOrDefault(
                                            o =>
                                                o.Name.Equals(
                                                    wasInput(
                                                        KeyValue.Get(
                                                            wasOutput(
                                                                Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                                                    .TYPE)),
                                                            corradeCommandParameters.Message)),
                                                    StringComparison.Ordinal));
                                    var pointAtType = (PointAtType?) pointAtTypeInfo?.GetValue(null) ??
                                                      PointAtType.None;
                                    // Check whether the specified UUID belongs to a different effect type.
                                    lock (LookAtEffectsLock)
                                    {
                                        if (LookAtEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.EFFECT_UUID_BELONGS_TO_DIFFERENT_EFFECT);
                                    }
                                    lock (BeamEffectsLock)
                                    {
                                        if (BeamEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.EFFECT_UUID_BELONGS_TO_DIFFERENT_EFFECT);
                                    }
                                    lock (SphereEffectsLock)
                                    {
                                        if (SphereEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.EFFECT_UUID_BELONGS_TO_DIFFERENT_EFFECT);
                                    }
                                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                                    Client.Self.PointAtEffect(Client.Self.AgentID, targetUUID, offset,
                                        pointAtType, effectUUID);
                                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                                    lock (PointAtEffectsLock)
                                    {
                                        if (PointAtEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                            PointAtEffects.RemoveWhere(o => o.Effect.Equals(effectUUID));
                                        if (!pointAtType.Equals(PointAtType.None))
                                            PointAtEffects.Add(new PointAtEffect
                                            {
                                                Effect = effectUUID,
                                                Offset = offset,
                                                Source = Client.Self.AgentID,
                                                Target = targetUUID,
                                                Type = pointAtType
                                            });
                                    }
                                    break;

                                case Enumerations.ViewerEffectType.BEAM:
                                case Enumerations.ViewerEffectType.SPHERE:
                                    Vector3 RGB;
                                    if (
                                        !Vector3.TryParse(
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                                        .COLOR)),
                                                    corradeCommandParameters.Message)),
                                            out RGB))
                                        RGB = new Vector3(Client.Settings.DEFAULT_EFFECT_COLOR.R,
                                            Client.Settings.DEFAULT_EFFECT_COLOR.G,
                                            Client.Settings.DEFAULT_EFFECT_COLOR.B);
                                    float alpha;
                                    if (!float.TryParse(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ALPHA)),
                                                corradeCommandParameters.Message)), NumberStyles.Float,
                                        Utils.EnUsCulture, out alpha))
                                        alpha = Client.Settings.DEFAULT_EFFECT_COLOR.A;
                                    float duration;
                                    if (
                                        !float.TryParse(
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(
                                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.DURATION)),
                                                    corradeCommandParameters.Message)), NumberStyles.Float,
                                            Utils.EnUsCulture,
                                            out duration))
                                        duration = 1;
                                    var color = new Color4(RGB.X, RGB.Y, RGB.Z, alpha);
                                    switch (viewerEffectType)
                                    {
                                        case Enumerations.ViewerEffectType.BEAM:
                                            // Check whether the specified UUID belongs to a different effect type.
                                            lock (LookAtEffectsLock)
                                            {
                                                if (LookAtEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                                    throw new Command.ScriptException(
                                                        Enumerations.ScriptError
                                                            .EFFECT_UUID_BELONGS_TO_DIFFERENT_EFFECT);
                                            }
                                            lock (PointAtEffectsLock)
                                            {
                                                if (PointAtEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                                    throw new Command.ScriptException(
                                                        Enumerations.ScriptError
                                                            .EFFECT_UUID_BELONGS_TO_DIFFERENT_EFFECT);
                                            }
                                            lock (SphereEffectsLock)
                                            {
                                                if (SphereEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                                    throw new Command.ScriptException(
                                                        Enumerations.ScriptError
                                                            .EFFECT_UUID_BELONGS_TO_DIFFERENT_EFFECT);
                                            }
                                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                                            Client.Self.BeamEffect(Client.Self.AgentID, targetUUID, offset,
                                                color, duration, effectUUID);
                                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                                            lock (BeamEffectsLock)
                                            {
                                                if (BeamEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                                    BeamEffects.RemoveWhere(o => o.Effect.Equals(effectUUID));
                                                BeamEffects.Add(new BeamEffect
                                                {
                                                    Effect = effectUUID,
                                                    Source = Client.Self.AgentID,
                                                    Target = targetUUID,
                                                    Color = new Vector3(color.R, color.G, color.B),
                                                    Alpha = color.A,
                                                    Duration = duration,
                                                    Offset = offset,
                                                    Termination = DateTime.UtcNow.AddSeconds(duration)
                                                });
                                            }
                                            break;

                                        case Enumerations.ViewerEffectType.SPHERE:
                                            // Check whether the specified UUID belongs to a different effect type.
                                            lock (LookAtEffectsLock)
                                            {
                                                if (LookAtEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                                    throw new Command.ScriptException(
                                                        Enumerations.ScriptError
                                                            .EFFECT_UUID_BELONGS_TO_DIFFERENT_EFFECT);
                                            }
                                            lock (PointAtEffectsLock)
                                            {
                                                if (PointAtEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                                    throw new Command.ScriptException(
                                                        Enumerations.ScriptError
                                                            .EFFECT_UUID_BELONGS_TO_DIFFERENT_EFFECT);
                                            }
                                            lock (BeamEffectsLock)
                                            {
                                                if (BeamEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                                    throw new Command.ScriptException(
                                                        Enumerations.ScriptError
                                                            .EFFECT_UUID_BELONGS_TO_DIFFERENT_EFFECT);
                                            }
                                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                                            Client.Self.SphereEffect(offset, color, duration,
                                                effectUUID);
                                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                                            lock (SphereEffectsLock)
                                            {
                                                if (SphereEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                                    SphereEffects.RemoveWhere(o => o.Effect.Equals(effectUUID));
                                                SphereEffects.Add(new SphereEffect
                                                {
                                                    Color = new Vector3(color.R, color.G, color.B),
                                                    Alpha = color.A,
                                                    Duration = duration,
                                                    Effect = effectUUID,
                                                    Offset = offset,
                                                    Termination = DateTime.UtcNow.AddSeconds(duration)
                                                });
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_EFFECT);
                    }
                    result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), effectUUID.ToString());
                };
        }
    }
}