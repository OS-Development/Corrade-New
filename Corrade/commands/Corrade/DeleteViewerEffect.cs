///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Structures.Effects;
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
                deleteviewereffect =
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
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_EFFECT_UUID_PROVIDED);
                        var viewerEffectType = Reflection.GetEnumValueFromName<Enumerations.ViewerEffectType>(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.EFFECT)),
                                    corradeCommandParameters.Message))
                        );
                        switch (viewerEffectType)
                        {
                            case Enumerations.ViewerEffectType.LOOK:
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
                                        throw new Command.ScriptException(Enumerations.ScriptError.EFFECT_NOT_FOUND);
                                }
                                Locks.ClientInstanceSelfLock.EnterWriteLock();
                                Client.Self.LookAtEffect(Client.Self.AgentID, Client.Self.AgentID,
                                    Vector3d.UnitX,
                                    LookAtType.Idle, effectUUID);
                                Locks.ClientInstanceSelfLock.ExitWriteLock();
                                break;

                            case Enumerations.ViewerEffectType.POINT:
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
                                        throw new Command.ScriptException(Enumerations.ScriptError.EFFECT_NOT_FOUND);
                                }
                                Locks.ClientInstanceSelfLock.EnterWriteLock();
                                Client.Self.PointAtEffect(Client.Self.AgentID, UUID.Zero,
                                    Vector3.Zero,
                                    PointAtType.None, effectUUID);
                                Locks.ClientInstanceSelfLock.ExitWriteLock();
                                lock (PointAtEffectsLock)
                                {
                                    PointAtEffects.Remove(pointAtEffect);
                                }
                                break;

                            case Enumerations.ViewerEffectType.BEAM:
                                BeamEffect beamEffect;
                                lock (BeamEffectsLock)
                                {
                                    beamEffect =
                                        BeamEffects.AsParallel().FirstOrDefault(o => o.Effect.Equals(effectUUID));
                                }
                                switch (!beamEffect.Equals(default(BeamEffect)))
                                {
                                    case false:
                                        throw new Command.ScriptException(Enumerations.ScriptError.EFFECT_NOT_FOUND);
                                }
                                Locks.ClientInstanceSelfLock.EnterWriteLock();
                                Client.Self.BeamEffect(Client.Self.AgentID, beamEffect.Target, Vector3.Zero,
                                    new Color4(beamEffect.Color.X, beamEffect.Color.Y, beamEffect.Color.Z,
                                        beamEffect.Alpha),
                                    0, effectUUID);
                                Locks.ClientInstanceSelfLock.ExitWriteLock();
                                lock (BeamEffectsLock)
                                {
                                    BeamEffects.Remove(beamEffect);
                                }
                                break;

                            case Enumerations.ViewerEffectType.SPHERE:
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
                                        throw new Command.ScriptException(Enumerations.ScriptError.EFFECT_NOT_FOUND);
                                }
                                Locks.ClientInstanceSelfLock.EnterWriteLock();
                                Client.Self.SphereEffect(Vector3.Zero,
                                    new Color4(sphereEffect.Color.X, sphereEffect.Color.Y, sphereEffect.Color.Z,
                                        sphereEffect.Alpha), 0, effectUUID);
                                Locks.ClientInstanceSelfLock.ExitWriteLock();
                                lock (SphereEffectsLock)
                                {
                                    SphereEffects.Remove(sphereEffect);
                                }
                                break;

                            default:
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_VIEWER_EFFECT);
                        }
                    };
        }
    }
}