///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> away =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    switch (Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                    ))
                    {
                        case Enumerations.Action.ENABLE:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.AnimationStart(Animations.AWAY, true);
                            Client.Self.Movement.Away = true;
                            Client.Self.Movement.SendUpdate(true);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        case Enumerations.Action.DISABLE:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.Movement.Away = false;
                            Client.Self.AnimationStop(Animations.AWAY, true);
                            Client.Self.Movement.SendUpdate(true);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        case Enumerations.Action.GET:
                            Locks.ClientInstanceSelfLock.EnterReadLock();
                            result.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                Client.Self.Movement.Away.ToString());
                            Locks.ClientInstanceSelfLock.ExitReadLock();
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}