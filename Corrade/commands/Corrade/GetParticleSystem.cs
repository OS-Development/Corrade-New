///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
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
                getparticlesystem =
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
                        var particleSystem = new StringBuilder();
                        particleSystem.Append("PSYS_PART_FLAGS, 0");
                        if (!((long) primitive.ParticleSys.PartDataFlags &
                              (long) Primitive.ParticleSystem.ParticleDataFlags.InterpColor).Equals(0))
                            particleSystem.Append(" | PSYS_PART_INTERP_COLOR_MASK");
                        if (!((long) primitive.ParticleSys.PartDataFlags &
                              (long) Primitive.ParticleSystem.ParticleDataFlags.InterpScale).Equals(0))
                            particleSystem.Append(" | PSYS_PART_INTERP_SCALE_MASK");
                        if (
                            !((long) primitive.ParticleSys.PartDataFlags &
                              (long) Primitive.ParticleSystem.ParticleDataFlags.Bounce).Equals(0))
                            particleSystem.Append(" | PSYS_PART_BOUNCE_MASK");
                        if (
                            !((long) primitive.ParticleSys.PartDataFlags &
                              (long) Primitive.ParticleSystem.ParticleDataFlags.Wind).Equals(0))
                            particleSystem.Append(" | PSYS_PART_WIND_MASK");
                        if (
                            !((long) primitive.ParticleSys.PartDataFlags &
                              (long) Primitive.ParticleSystem.ParticleDataFlags.FollowSrc).Equals(0))
                            particleSystem.Append(" | PSYS_PART_FOLLOW_SRC_MASK");
                        if (!((long) primitive.ParticleSys.PartDataFlags &
                              (long) Primitive.ParticleSystem.ParticleDataFlags.FollowVelocity).Equals(0))
                            particleSystem.Append(" | PSYS_PART_FOLLOW_VELOCITY_MASK");
                        if (
                            !((long) primitive.ParticleSys.PartDataFlags &
                              (long) Primitive.ParticleSystem.ParticleDataFlags.TargetPos).Equals(0))
                            particleSystem.Append(" | PSYS_PART_TARGET_POS_MASK");
                        if (!((long) primitive.ParticleSys.PartDataFlags &
                              (long) Primitive.ParticleSystem.ParticleDataFlags.TargetLinear).Equals(0))
                            particleSystem.Append(" | PSYS_PART_TARGET_LINEAR_MASK");
                        if (
                            !((long) primitive.ParticleSys.PartDataFlags &
                              (long) Primitive.ParticleSystem.ParticleDataFlags.Emissive).Equals(0))
                            particleSystem.Append(" | PSYS_PART_EMISSIVE_MASK");
                        particleSystem.Append(",");
                        particleSystem.Append("PSYS_SRC_PATTERN, 0");
                        if (
                            !((long) primitive.ParticleSys.Pattern & (long) Primitive.ParticleSystem.SourcePattern.Drop)
                                .Equals(0))
                            particleSystem.Append(" | PSYS_SRC_PATTERN_DROP");
                        if (!((long) primitive.ParticleSys.Pattern &
                              (long) Primitive.ParticleSystem.SourcePattern.Explode).Equals(0))
                            particleSystem.Append(" | PSYS_SRC_PATTERN_EXPLODE");
                        if (
                            !((long) primitive.ParticleSys.Pattern &
                              (long) Primitive.ParticleSystem.SourcePattern.Angle)
                                .Equals(0))
                            particleSystem.Append(" | PSYS_SRC_PATTERN_ANGLE");
                        if (!((long) primitive.ParticleSys.Pattern &
                              (long) Primitive.ParticleSystem.SourcePattern.AngleCone).Equals(0))
                            particleSystem.Append(" | PSYS_SRC_PATTERN_ANGLE_CONE");
                        if (!((long) primitive.ParticleSys.Pattern &
                              (long) Primitive.ParticleSystem.SourcePattern.AngleConeEmpty).Equals(0))
                            particleSystem.Append(" | PSYS_SRC_PATTERN_ANGLE_CONE_EMPTY");
                        particleSystem.Append(",");
                        particleSystem.Append("PSYS_PART_START_ALPHA, " +
                                              string.Format(Utils.EnUsCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.PartStartColor.A) +
                                              ",");
                        particleSystem.Append("PSYS_PART_END_ALPHA, " +
                                              string.Format(Utils.EnUsCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.PartEndColor.A) +
                                              ",");
                        particleSystem.Append("PSYS_PART_START_COLOR, " +
                                              primitive.ParticleSys.PartStartColor.ToRGBString() +
                                              ",");
                        particleSystem.Append("PSYS_PART_END_COLOR, " +
                                              primitive.ParticleSys.PartEndColor.ToRGBString() +
                                              ",");
                        particleSystem.Append("PSYS_PART_START_SCALE, <" +
                                              string.Format(Utils.EnUsCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.PartStartScaleX) + ", " +
                                              string.Format(Utils.EnUsCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.PartStartScaleY) +
                                              ", 0>, ");
                        particleSystem.Append("PSYS_PART_END_SCALE, <" +
                                              string.Format(Utils.EnUsCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.PartEndScaleX) + ", " +
                                              string.Format(Utils.EnUsCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.PartEndScaleY) +
                                              ", 0>, ");
                        particleSystem.Append("PSYS_PART_MAX_AGE, " +
                                              string.Format(Utils.EnUsCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.PartMaxAge) +
                                              ",");
                        particleSystem.Append("PSYS_SRC_MAX_AGE, " +
                                              string.Format(Utils.EnUsCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.MaxAge) +
                                              ",");
                        particleSystem.Append("PSYS_SRC_ACCEL, " + primitive.ParticleSys.PartAcceleration +
                                              ",");
                        particleSystem.Append("PSYS_SRC_BURST_PART_COUNT, " +
                                              string.Format(Utils.EnUsCulture, "{0:0}",
                                                  primitive.ParticleSys.BurstPartCount) +
                                              ",");
                        particleSystem.Append("PSYS_SRC_BURST_RADIUS, " +
                                              string.Format(Utils.EnUsCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.BurstRadius) +
                                              ",");
                        particleSystem.Append("PSYS_SRC_BURST_RATE, " +
                                              string.Format(Utils.EnUsCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.BurstRate) +
                                              ",");
                        particleSystem.Append("PSYS_SRC_BURST_SPEED_MIN, " +
                                              string.Format(Utils.EnUsCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.BurstSpeedMin) +
                                              ",");
                        particleSystem.Append("PSYS_SRC_BURST_SPEED_MAX, " +
                                              string.Format(Utils.EnUsCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.BurstSpeedMax) +
                                              ",");
                        particleSystem.Append("PSYS_SRC_INNERANGLE, " +
                                              string.Format(Utils.EnUsCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.InnerAngle) +
                                              ",");
                        particleSystem.Append("PSYS_SRC_OUTERANGLE, " +
                                              string.Format(Utils.EnUsCulture, "{0:0.00000}",
                                                  primitive.ParticleSys.OuterAngle) +
                                              ",");
                        particleSystem.Append("PSYS_SRC_OMEGA, " + primitive.ParticleSys.AngularVelocity +
                                              ",");
                        particleSystem.Append("PSYS_SRC_TEXTURE, (key)\"" + primitive.ParticleSys.Texture + "\"" +
                                              ",");
                        particleSystem.Append("PSYS_SRC_TARGET_KEY, (key)\"" + primitive.ParticleSys.Target + "\"");
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), particleSystem.ToString());
                    };
        }
    }
}