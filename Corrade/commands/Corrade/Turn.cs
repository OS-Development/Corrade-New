///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;
using wasOpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> turn =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Movement))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    float degrees;
                    if (!float.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DEGREES)),
                            corradeCommandParameters.Message)), out degrees))
                    {
                        throw new ScriptException(ScriptError.INVALID_ANGLE_PROVIDED);
                    }
                    switch (Reflection.GetEnumValueFromName<Direction>(
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DIRECTION)),
                            corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Direction.LEFT:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.Movement.BodyRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitZ,
                                    degrees);
                                Client.Self.Movement.HeadRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitZ,
                                    degrees);
                                Client.Self.Movement.SendManualUpdate(
                                    (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                    AgentManager.ControlFlags.
                                        AGENT_CONTROL_TURN_LEFT, Client.Self.Movement.Camera.Position,
                                    Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                    Client.Self.Movement.Camera.UpAxis,
                                    Client.Self.Movement.BodyRotation,
                                    Client.Self.Movement.HeadRotation,
                                    Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                    Client.Self.Movement.State, false);
                            }
                            break;
                        case Direction.RIGHT:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.Movement.BodyRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitZ,
                                    -degrees);
                                Client.Self.Movement.HeadRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitZ,
                                    -degrees);
                                Client.Self.Movement.SendManualUpdate(
                                    (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                    AgentManager.ControlFlags.
                                        AGENT_CONTROL_TURN_RIGHT, Client.Self.Movement.Camera.Position,
                                    Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                    Client.Self.Movement.Camera.UpAxis,
                                    Client.Self.Movement.BodyRotation,
                                    Client.Self.Movement.HeadRotation,
                                    Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                    Client.Self.Movement.State, false);
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_DIRECTION);
                    }
                    // Set the camera on the avatar.
                    lock (Locks.ClientInstanceSelfLock)
                    {
                        Client.Self.Movement.Camera.LookAt(
                            Client.Self.SimPosition,
                            Client.Self.SimPosition
                            );
                    }
                };
        }
    }
}