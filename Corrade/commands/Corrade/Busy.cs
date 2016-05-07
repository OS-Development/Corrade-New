///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> busy =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    switch (Reflection.GetEnumValueFromName<Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Action.ENABLE:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.AnimationStart(Animations.BUSY, true);
                            }
                            break;
                        case Action.DISABLE:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.AnimationStop(Animations.BUSY, true);
                            }
                            break;
                        case Action.GET:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                result.Add(Reflection.GetNameFromEnumValue(ScriptKeys.DATA),
                                    Client.Self.SignaledAnimations.ContainsKey(Animations.BUSY).ToString());
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}