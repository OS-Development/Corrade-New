///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> setviewereffect =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    UUID effectUUID;
                    if (!UUID.TryParse(wasInput(wasKeyValueGet(
                        wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ID)), corradeCommandParameters.Message)),
                        out effectUUID))
                    {
                        effectUUID = UUID.Random();
                    }
                    Vector3 offset;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                wasKeyValueGet(
                                    wasOutput(
                                        wasGetDescriptionFromEnumValue(ScriptKeys.OFFSET)),
                                    corradeCommandParameters.Message)),
                            out offset))
                    {
                        offset = Client.Self.SimPosition;
                    }
                    ViewerEffectType viewerEffectType = wasGetEnumValueFromDescription<ViewerEffectType>(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.EFFECT)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant());
                    switch (viewerEffectType)
                    {
                        case ViewerEffectType.BEAM:
                        case ViewerEffectType.POINT:
                        case ViewerEffectType.LOOK:
                            string item = wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)),
                                corradeCommandParameters.Message));
                            UUID targetUUID;
                            switch (!string.IsNullOrEmpty(item))
                            {
                                case true:
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
                                            StringOrUUID(item),
                                            range,
                                            ref primitive, corradeConfiguration.ServicesTimeout,
                                            corradeConfiguration.DataTimeout))
                                    {
                                        throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                                    }
                                    targetUUID = primitive.ID;
                                    break;
                                default:
                                    if (
                                        !UUID.TryParse(
                                            wasInput(wasKeyValueGet(
                                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT)),
                                                corradeCommandParameters.Message)),
                                            out targetUUID) && !AgentNameToUUID(
                                                wasInput(
                                                    wasKeyValueGet(
                                                        wasOutput(
                                                            wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME)),
                                                        corradeCommandParameters.Message)),
                                                wasInput(
                                                    wasKeyValueGet(
                                                        wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME)),
                                                        corradeCommandParameters.Message)),
                                                corradeConfiguration.ServicesTimeout,
                                                corradeConfiguration.DataTimeout,
                                                ref targetUUID))
                                    {
                                        throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                                    }
                                    break;
                            }
                            switch (viewerEffectType)
                            {
                                case ViewerEffectType.LOOK:
                                    FieldInfo lookAtTypeInfo = typeof (LookAtType).GetFields(BindingFlags.Public |
                                                                                             BindingFlags.Static)
                                        .AsParallel().FirstOrDefault(
                                            o =>
                                                o.Name.Equals(
                                                    wasInput(
                                                        wasKeyValueGet(
                                                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)),
                                                            corradeCommandParameters.Message)),
                                                    StringComparison.Ordinal));
                                    LookAtType lookAtType = lookAtTypeInfo != null
                                        ? (LookAtType)
                                            lookAtTypeInfo
                                                .GetValue(null)
                                        : LookAtType.None;
                                    Client.Self.LookAtEffect(Client.Self.AgentID, targetUUID, offset,
                                        lookAtType, effectUUID);
                                    if (LookAtEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                    {
                                        LookAtEffects.RemoveWhere(o => o.Effect.Equals(effectUUID));
                                    }
                                    if (!lookAtType.Equals(LookAtType.None))
                                    {
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
                                case ViewerEffectType.POINT:
                                    FieldInfo pointAtTypeInfo = typeof (PointAtType).GetFields(BindingFlags.Public |
                                                                                               BindingFlags.Static)
                                        .AsParallel().FirstOrDefault(
                                            o =>
                                                o.Name.Equals(
                                                    wasInput(
                                                        wasKeyValueGet(
                                                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)),
                                                            corradeCommandParameters.Message)),
                                                    StringComparison.Ordinal));
                                    PointAtType pointAtType = pointAtTypeInfo != null
                                        ? (PointAtType)
                                            pointAtTypeInfo
                                                .GetValue(null)
                                        : PointAtType.None;
                                    Client.Self.PointAtEffect(Client.Self.AgentID, targetUUID, offset,
                                        pointAtType, effectUUID);
                                    if (PointAtEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                    {
                                        PointAtEffects.RemoveWhere(o => o.Effect.Equals(effectUUID));
                                    }
                                    if (!pointAtType.Equals(PointAtType.None))
                                    {
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
                                case ViewerEffectType.BEAM:
                                case ViewerEffectType.SPHERE:
                                    Vector3 RGB;
                                    if (
                                        !Vector3.TryParse(
                                            wasInput(
                                                wasKeyValueGet(
                                                    wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.COLOR)),
                                                    corradeCommandParameters.Message)),
                                            out RGB))
                                    {
                                        RGB = new Vector3(Client.Settings.DEFAULT_EFFECT_COLOR.R,
                                            Client.Settings.DEFAULT_EFFECT_COLOR.G,
                                            Client.Settings.DEFAULT_EFFECT_COLOR.B);
                                    }
                                    float alpha;
                                    if (!float.TryParse(
                                        wasInput(
                                            wasKeyValueGet(
                                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ALPHA)),
                                                corradeCommandParameters.Message)), out alpha))
                                    {
                                        alpha = Client.Settings.DEFAULT_EFFECT_COLOR.A;
                                    }
                                    float duration;
                                    if (
                                        !float.TryParse(
                                            wasInput(
                                                wasKeyValueGet(
                                                    wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DURATION)),
                                                    corradeCommandParameters.Message)),
                                            out duration))
                                    {
                                        duration = 1;
                                    }
                                    Color4 color = new Color4(RGB.X, RGB.Y, RGB.Z, alpha);
                                    switch (viewerEffectType)
                                    {
                                        case ViewerEffectType.BEAM:
                                            Client.Self.BeamEffect(Client.Self.AgentID, targetUUID, offset,
                                                color, duration, effectUUID);
                                            lock (BeamEffectsLock)
                                            {
                                                if (BeamEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                                {
                                                    BeamEffects.RemoveWhere(o => o.Effect.Equals(effectUUID));
                                                }
                                                BeamEffects.Add(new BeamEffect
                                                {
                                                    Effect = effectUUID,
                                                    Source = Client.Self.AgentID,
                                                    Target = targetUUID,
                                                    Color = new Vector3(color.R, color.G, color.B),
                                                    Alpha = color.A,
                                                    Duration = duration,
                                                    Offset = offset,
                                                    Termination = DateTime.Now.AddSeconds(duration)
                                                });
                                            }
                                            break;
                                        case ViewerEffectType.SPHERE:
                                            Client.Self.SphereEffect(offset, color, duration,
                                                effectUUID);
                                            lock (SphereEffectsLock)
                                            {
                                                if (SphereEffects.AsParallel().Any(o => o.Effect.Equals(effectUUID)))
                                                {
                                                    SphereEffects.RemoveWhere(o => o.Effect.Equals(effectUUID));
                                                }
                                                SphereEffects.Add(new SphereEffect
                                                {
                                                    Color = new Vector3(color.R, color.G, color.B),
                                                    Alpha = color.A,
                                                    Duration = duration,
                                                    Effect = effectUUID,
                                                    Offset = offset,
                                                    Termination = DateTime.Now.AddSeconds(duration)
                                                });
                                            }
                                            break;
                                    }
                                    break;
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_EFFECT);
                    }
                    result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA), effectUUID.ToString());
                };
        }
    }
}