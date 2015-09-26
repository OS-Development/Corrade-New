///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getviewereffects =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    List<string> csv = new List<string>();
                    object LockObject = new object();
                    ViewerEffectType viewerEffectType = wasGetEnumValueFromDescription<ViewerEffectType>(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.EFFECT)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant());
                    switch (viewerEffectType)
                    {
                        case ViewerEffectType.LOOK:
                            Parallel.ForEach(LookAtEffects, o =>
                            {
                                lock (LockObject)
                                {
                                    csv.AddRange(new[]
                                    {wasGetStructureMemberDescription(o, o.Effect), o.Effect.ToString()});
                                    csv.AddRange(new[]
                                    {wasGetStructureMemberDescription(o, o.Source), o.Source.ToString()});
                                    csv.AddRange(new[]
                                    {wasGetStructureMemberDescription(o, o.Target), o.Target.ToString()});
                                    csv.AddRange(new[]
                                    {wasGetStructureMemberDescription(o, o.Offset), o.Offset.ToString()});
                                    csv.AddRange(new[]
                                    {
                                        wasGetStructureMemberDescription(o, o.Type),
                                        Enum.GetName(typeof (LookAtType), o.Type)
                                    });
                                }
                            });
                            break;
                        case ViewerEffectType.POINT:
                            Parallel.ForEach(PointAtEffects, o =>
                            {
                                lock (LockObject)
                                {
                                    csv.AddRange(new[]
                                    {wasGetStructureMemberDescription(o, o.Effect), o.Effect.ToString()});
                                    csv.AddRange(new[]
                                    {wasGetStructureMemberDescription(o, o.Source), o.Source.ToString()});
                                    csv.AddRange(new[]
                                    {wasGetStructureMemberDescription(o, o.Target), o.Target.ToString()});
                                    csv.AddRange(new[]
                                    {wasGetStructureMemberDescription(o, o.Offset), o.Offset.ToString()});
                                    csv.AddRange(new[]
                                    {
                                        wasGetStructureMemberDescription(o, o.Type),
                                        Enum.GetName(typeof (PointAtType), o.Type)
                                    });
                                }
                            });
                            break;
                        case ViewerEffectType.SPHERE:
                            lock (SphereEffectsLock)
                            {
                                Parallel.ForEach(SphereEffects, o =>
                                {
                                    lock (LockObject)
                                    {
                                        csv.AddRange(new[]
                                        {wasGetStructureMemberDescription(o, o.Effect), o.Effect.ToString()});
                                        csv.AddRange(new[]
                                        {wasGetStructureMemberDescription(o, o.Offset), o.Offset.ToString()});
                                        csv.AddRange(new[]
                                        {wasGetStructureMemberDescription(o, o.Color), o.Color.ToString()});
                                        csv.AddRange(new[]
                                        {
                                            wasGetStructureMemberDescription(o, o.Alpha),
                                            o.Alpha.ToString(Utils.EnUsCulture)
                                        });
                                        csv.AddRange(new[]
                                        {
                                            wasGetStructureMemberDescription(o, o.Duration),
                                            o.Duration.ToString(Utils.EnUsCulture)
                                        });
                                        csv.AddRange(new[]
                                        {
                                            wasGetStructureMemberDescription(o, o.Termination),
                                            o.Termination.ToString(Utils.EnUsCulture)
                                        });
                                    }
                                });
                            }
                            break;
                        case ViewerEffectType.BEAM:
                            lock (BeamEffectsLock)
                            {
                                Parallel.ForEach(BeamEffects, o =>
                                {
                                    lock (LockObject)
                                    {
                                        csv.AddRange(new[]
                                        {wasGetStructureMemberDescription(o, o.Effect), o.Effect.ToString()});
                                        csv.AddRange(new[]
                                        {wasGetStructureMemberDescription(o, o.Offset), o.Offset.ToString()});
                                        csv.AddRange(new[]
                                        {wasGetStructureMemberDescription(o, o.Source), o.Source.ToString()});
                                        csv.AddRange(new[]
                                        {wasGetStructureMemberDescription(o, o.Target), o.Target.ToString()});
                                        csv.AddRange(new[]
                                        {wasGetStructureMemberDescription(o, o.Color), o.Color.ToString()});
                                        csv.AddRange(new[]
                                        {
                                            wasGetStructureMemberDescription(o, o.Alpha),
                                            o.Alpha.ToString(Utils.EnUsCulture)
                                        });
                                        csv.AddRange(new[]
                                        {
                                            wasGetStructureMemberDescription(o, o.Duration),
                                            o.Duration.ToString(Utils.EnUsCulture)
                                        });
                                        csv.AddRange(new[]
                                        {
                                            wasGetStructureMemberDescription(o, o.Termination),
                                            o.Termination.ToString(Utils.EnUsCulture)
                                        });
                                    }
                                });
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_EFFECT);
                    }
                    if (csv.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(csv));
                    }
                };
        }
    }
}