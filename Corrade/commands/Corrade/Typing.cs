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
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> typing =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Enumerations.Action.ENABLE:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.AnimationStart(Animations.TYPE, true);
                            }
                            break;
                        case Enumerations.Action.DISABLE:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.AnimationStop(Animations.TYPE, true);
                            }
                            break;
                        case Enumerations.Action.GET:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                result.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                    Client.Self.SignaledAnimations.ContainsKey(Animations.TYPE).ToString());
                            }
                            break;
                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}