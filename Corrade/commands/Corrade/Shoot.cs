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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> shoot =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    // Turn toward the target.
                    Vector3 position;
                    if (
                        Vector3.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                corradeCommandParameters.Message)),
                            out position))
                    {
                        Locks.ClientInstanceSelfLock.EnterWriteLock();
                        Client.Self.Movement.TurnToward(position);
                        Locks.ClientInstanceSelfLock.ExitWriteLock();
                    }
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    // Go to mouselook and press LMB button.
                    Client.Self.Movement.Mouselook = true;
                    Client.Self.Movement.MLButtonDown = true;
                    Client.Self.Movement.SendUpdate();

                    // Release LMB and finish animation.
                    Client.Self.Movement.MLButtonUp = true;
                    Client.Self.Movement.MLButtonDown = false;
                    Client.Self.Movement.FinishAnim = true;
                    Client.Self.Movement.SendUpdate();

                    // Get out of mouse look.
                    Client.Self.Movement.Mouselook = false;
                    Client.Self.Movement.MLButtonUp = false;
                    Client.Self.Movement.FinishAnim = false;
                    Client.Self.Movement.SendUpdate();

                    // Set the camera on the avatar.
                    Client.Self.Movement.Camera.LookAt(
                        Client.Self.SimPosition,
                        Client.Self.SimPosition
                    );
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                };
        }
    }
}