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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> deleteviewereffect =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    UUID effectUUID;
                    if (!UUID.TryParse(wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ID)), corradeCommandParameters.Message)),
                        out effectUUID))
                    {
                        throw new ScriptException(ScriptError.NO_EFFECT_UUID_PROVIDED);
                    }
                    ViewerEffectType viewerEffectType = Reflection.GetEnumValueFromName<ViewerEffectType>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.EFFECT)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant());
                    switch (viewerEffectType)
                    {
                        case ViewerEffectType.LOOK:
                            LookAtEffect lookAtEffect;
                            lock (LookAtEffectsLock)
                            {
                                lookAtEffect =
                                    LookAtEffects
                                        .AsParallel()
                                        .FirstOrDefault(o => o.Effect.Equals(effectUUID));
                            }
                            switch (!lookAtEffect.Equals(default(LookAtEffect)))
                            {
                                case false:
                                    throw new ScriptException(ScriptError.EFFECT_NOT_FOUND);
                            }
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.LookAtEffect(Client.Self.AgentID, Client.Self.AgentID,
                                    Vector3d.UnitX,
                                    LookAtType.Idle, effectUUID);
                            }
                            break;
                        case ViewerEffectType.POINT:
                            PointAtEffect pointAtEffect;
                            lock (PointAtEffectsLock)
                            {
                                pointAtEffect =
                                    PointAtEffects
                                        .AsParallel()
                                        .FirstOrDefault(o => o.Effect.Equals(effectUUID));
                            }
                            switch (!pointAtEffect.Equals(default(PointAtEffect)))
                            {
                                case false:
                                    throw new ScriptException(ScriptError.EFFECT_NOT_FOUND);
                            }
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.PointAtEffect(Client.Self.AgentID, UUID.Zero,
                                    Vector3.Zero,
                                    PointAtType.None, effectUUID);
                            }
                            lock (PointAtEffectsLock)
                            {
                                PointAtEffects.Remove(pointAtEffect);
                            }
                            break;
                        case ViewerEffectType.BEAM:
                            BeamEffect beamEffect;
                            lock (BeamEffectsLock)
                            {
                                beamEffect =
                                    BeamEffects.AsParallel().FirstOrDefault(o => o.Effect.Equals(effectUUID));
                            }
                            switch (!beamEffect.Equals(default(BeamEffect)))
                            {
                                case false:
                                    throw new ScriptException(ScriptError.EFFECT_NOT_FOUND);
                            }
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.BeamEffect(Client.Self.AgentID, beamEffect.Target, Vector3.Zero,
                                    new Color4(beamEffect.Color.X, beamEffect.Color.Y, beamEffect.Color.Z,
                                        beamEffect.Alpha),
                                    0, effectUUID);
                            }
                            lock (BeamEffectsLock)
                            {
                                BeamEffects.Remove(beamEffect);
                            }
                            break;
                        case ViewerEffectType.SPHERE:
                            SphereEffect sphereEffect;
                            lock (SphereEffectsLock)
                            {
                                sphereEffect =
                                    SphereEffects
                                        .AsParallel()
                                        .FirstOrDefault(o => o.Effect.Equals(effectUUID));
                            }
                            switch (!sphereEffect.Equals(default(SphereEffect)))
                            {
                                case false:
                                    throw new ScriptException(ScriptError.EFFECT_NOT_FOUND);
                            }
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.SphereEffect(Vector3.Zero,
                                    new Color4(sphereEffect.Color.X, sphereEffect.Color.Y, sphereEffect.Color.Z,
                                        sphereEffect.Alpha), 0, effectUUID);
                            }
                            lock (SphereEffectsLock)
                            {
                                SphereEffects.Remove(sphereEffect);
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.INVALID_VIEWER_EFFECT);
                    }
                };
        }
    }
}