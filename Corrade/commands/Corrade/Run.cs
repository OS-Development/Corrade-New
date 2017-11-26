///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using CorradeConfigurationSharp;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> run =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Movement))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var action = Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                    );
                    switch (action)
                    {
                        case Enumerations.Action.ENABLE:
                        case Enumerations.Action.DISABLE:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.Movement.AlwaysRun = !action.Equals(Enumerations.Action.DISABLE);
                            Client.Self.Movement.SendUpdate(true);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        case Enumerations.Action.GET:
                            Locks.ClientInstanceSelfLock.EnterReadLock();
                            result.Add(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA),
                                Client.Self.Movement.AlwaysRun.ToString());
                            Locks.ClientInstanceSelfLock.ExitReadLock();
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}