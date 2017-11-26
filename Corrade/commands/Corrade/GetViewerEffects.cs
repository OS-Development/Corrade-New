///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getviewereffects
                =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var csv = new List<string>();
                    var LockObject = new object();
                    var viewerEffectType = Reflection.GetEnumValueFromName<Enumerations.ViewerEffectType>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.EFFECT)),
                                corradeCommandParameters.Message))
                    );
                    switch (viewerEffectType)
                    {
                        case Enumerations.ViewerEffectType.LOOK:
                            LookAtEffects.AsParallel().ForAll(o =>
                            {
                                lock (LockObject)
                                {
                                    csv.AddRange(new[]
                                        {Reflection.GetStructureMemberName(o, o.Effect), o.Effect.ToString()});
                                    csv.AddRange(new[]
                                        {Reflection.GetStructureMemberName(o, o.Source), o.Source.ToString()});
                                    csv.AddRange(new[]
                                        {Reflection.GetStructureMemberName(o, o.Target), o.Target.ToString()});
                                    csv.AddRange(new[]
                                        {Reflection.GetStructureMemberName(o, o.Offset), o.Offset.ToString()});
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetStructureMemberName(o, o.Type),
                                        Enum.GetName(typeof(LookAtType), o.Type)
                                    });
                                }
                            });
                            break;

                        case Enumerations.ViewerEffectType.POINT:
                            PointAtEffects.AsParallel().ForAll(o =>
                            {
                                lock (LockObject)
                                {
                                    csv.AddRange(new[]
                                        {Reflection.GetStructureMemberName(o, o.Effect), o.Effect.ToString()});
                                    csv.AddRange(new[]
                                        {Reflection.GetStructureMemberName(o, o.Source), o.Source.ToString()});
                                    csv.AddRange(new[]
                                        {Reflection.GetStructureMemberName(o, o.Target), o.Target.ToString()});
                                    csv.AddRange(new[]
                                        {Reflection.GetStructureMemberName(o, o.Offset), o.Offset.ToString()});
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetStructureMemberName(o, o.Type),
                                        Enum.GetName(typeof(PointAtType), o.Type)
                                    });
                                }
                            });
                            break;

                        case Enumerations.ViewerEffectType.SPHERE:
                            lock (SphereEffectsLock)
                            {
                                SphereEffects.AsParallel().ForAll(o =>
                                {
                                    lock (LockObject)
                                    {
                                        csv.AddRange(new[]
                                            {Reflection.GetStructureMemberName(o, o.Effect), o.Effect.ToString()});
                                        csv.AddRange(new[]
                                            {Reflection.GetStructureMemberName(o, o.Offset), o.Offset.ToString()});
                                        csv.AddRange(new[]
                                            {Reflection.GetStructureMemberName(o, o.Color), o.Color.ToString()});
                                        csv.AddRange(new[]
                                        {
                                            Reflection.GetStructureMemberName(o, o.Alpha),
                                            o.Alpha.ToString(Utils.EnUsCulture)
                                        });
                                        csv.AddRange(new[]
                                        {
                                            Reflection.GetStructureMemberName(o, o.Duration),
                                            o.Duration.ToString(Utils.EnUsCulture)
                                        });
                                        csv.AddRange(new[]
                                        {
                                            Reflection.GetStructureMemberName(o, o.Termination),
                                            o.Termination.ToString(Utils.EnUsCulture)
                                        });
                                    }
                                });
                            }
                            break;

                        case Enumerations.ViewerEffectType.BEAM:
                            lock (BeamEffectsLock)
                            {
                                BeamEffects.AsParallel().ForAll(o =>
                                {
                                    lock (LockObject)
                                    {
                                        csv.AddRange(new[]
                                            {Reflection.GetStructureMemberName(o, o.Effect), o.Effect.ToString()});
                                        csv.AddRange(new[]
                                            {Reflection.GetStructureMemberName(o, o.Offset), o.Offset.ToString()});
                                        csv.AddRange(new[]
                                            {Reflection.GetStructureMemberName(o, o.Source), o.Source.ToString()});
                                        csv.AddRange(new[]
                                            {Reflection.GetStructureMemberName(o, o.Target), o.Target.ToString()});
                                        csv.AddRange(new[]
                                            {Reflection.GetStructureMemberName(o, o.Color), o.Color.ToString()});
                                        csv.AddRange(new[]
                                        {
                                            Reflection.GetStructureMemberName(o, o.Alpha),
                                            o.Alpha.ToString(Utils.EnUsCulture)
                                        });
                                        csv.AddRange(new[]
                                        {
                                            Reflection.GetStructureMemberName(o, o.Duration),
                                            o.Duration.ToString(Utils.EnUsCulture)
                                        });
                                        csv.AddRange(new[]
                                        {
                                            Reflection.GetStructureMemberName(o, o.Termination),
                                            o.Termination.ToString(Utils.EnUsCulture)
                                        });
                                    }
                                });
                            }
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_EFFECT);
                    }
                    if (csv.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                };
        }
    }
}