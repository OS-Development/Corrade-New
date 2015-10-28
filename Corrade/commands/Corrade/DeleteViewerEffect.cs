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
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    UUID effectUUID;
                    if (!UUID.TryParse(wasInput(KeyValue.wasKeyValueGet(
                        wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.ID)), corradeCommandParameters.Message)),
                        out effectUUID))
                    {
                        throw new ScriptException(ScriptError.NO_EFFECT_UUID_PROVIDED);
                    }
                    ViewerEffectType viewerEffectType = Reflection.wasGetEnumValueFromName<ViewerEffectType>(
                        wasInput(
                            KeyValue.wasKeyValueGet(wasOutput(Reflection.wasGetNameFromEnumValue(ScriptKeys.EFFECT)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant());
                    switch (viewerEffectType)
                    {
                        case ViewerEffectType.LOOK:
                            LookAtEffect lookAtEffect =
                                LookAtEffects.AsParallel().FirstOrDefault(o => o.Effect.Equals(effectUUID));
                            switch (!lookAtEffect.Equals(default(LookAtEffect)))
                            {
                                case false:
                                    throw new ScriptException(ScriptError.EFFECT_NOT_FOUND);
                            }
                            Client.Self.LookAtEffect(Client.Self.AgentID, lookAtEffect.Target, Vector3.Zero,
                                LookAtType.None, effectUUID);
                            LookAtEffects.Remove(lookAtEffect);
                            break;
                        case ViewerEffectType.POINT:
                            PointAtEffect pointAtEffect =
                                PointAtEffects.AsParallel().FirstOrDefault(o => o.Effect.Equals(effectUUID));
                            switch (!pointAtEffect.Equals(default(PointAtEffect)))
                            {
                                case false:
                                    throw new ScriptException(ScriptError.EFFECT_NOT_FOUND);
                            }
                            Client.Self.PointAtEffect(Client.Self.AgentID, pointAtEffect.Target,
                                Vector3.Zero,
                                PointAtType.None, effectUUID);
                            PointAtEffects.Remove(pointAtEffect);
                            break;
                        default:
                            throw new ScriptException(ScriptError.INVALID_VIEWER_EFFECT);
                    }
                };
        }
    }
}