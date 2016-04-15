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
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> setcameradata =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    AgentManager.AgentMovement.AgentCamera camera = Client.Self.Movement.Camera;
                    wasCSVToStructure(
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                            corradeCommandParameters.Message)),
                        ref camera);
                    lock (Locks.ClientInstanceSelfLock)
                    {
                        Client.Self.Movement.Camera.AtAxis = camera.AtAxis;
                        Client.Self.Movement.Camera.Far = camera.Far;
                        Client.Self.Movement.Camera.LeftAxis = camera.LeftAxis;
                        Client.Self.Movement.Camera.Position = camera.Position;
                        Client.Self.Movement.Camera.UpAxis = camera.UpAxis;
                        // Send update.
                        Client.Self.Movement.SendUpdate(true);
                    }
                    // Save movement state.
                    SaveMovementState.Invoke();
                };
        }
    }
}