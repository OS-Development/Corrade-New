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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> setcameradata =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    Locks.ClientInstanceSelfLock.EnterReadLock();
                    var camera = Client.Self.Movement.Camera;
                    Locks.ClientInstanceSelfLock.ExitReadLock();
                    camera = camera.wasCSVToStructure(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                            corradeCommandParameters.Message)), wasInput);
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.Movement.Camera.AtAxis = camera.AtAxis;
                    Client.Self.Movement.Camera.Far = camera.Far;
                    Client.Self.Movement.Camera.LeftAxis = camera.LeftAxis;
                    Client.Self.Movement.Camera.Position = camera.Position;
                    Client.Self.Movement.Camera.UpAxis = camera.UpAxis;
                    // Send update.
                    Client.Self.Movement.SendUpdate(true);
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                    // Save movement state.
                    SaveMovementState.Invoke();
                };
        }
    }
}