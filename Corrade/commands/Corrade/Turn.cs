///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> turn =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Movement))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    float radians;
                    if (!float.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RADIANS)),
                            corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture, out radians))
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ANGLE_PROVIDED);
                    // Convert angle in radians to degrees.
                    switch (Reflection.GetEnumValueFromName<Enumerations.Direction>(
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DIRECTION)),
                            corradeCommandParameters.Message))
                    ))
                    {
                        case Enumerations.Direction.LEFT:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.Movement.BodyRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitZ,
                                radians);
                            Client.Self.Movement.HeadRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitZ,
                                radians);
                            Client.Self.Movement.SendManualUpdate(
                                (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                AgentManager.ControlFlags.AGENT_CONTROL_TURN_LEFT, Client.Self.Movement.Camera.Position,
                                Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                Client.Self.Movement.Camera.UpAxis,
                                Client.Self.Movement.BodyRotation,
                                Client.Self.Movement.HeadRotation,
                                Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                Client.Self.Movement.State, false);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        case Enumerations.Direction.RIGHT:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.Movement.BodyRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitZ,
                                -radians);
                            Client.Self.Movement.HeadRotation *= Quaternion.CreateFromAxisAngle(Vector3.UnitZ,
                                -radians);
                            Client.Self.Movement.SendManualUpdate(
                                (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                AgentManager.ControlFlags.AGENT_CONTROL_TURN_RIGHT,
                                Client.Self.Movement.Camera.Position,
                                Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                Client.Self.Movement.Camera.UpAxis,
                                Client.Self.Movement.BodyRotation,
                                Client.Self.Movement.HeadRotation,
                                Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                Client.Self.Movement.State, false);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_DIRECTION);
                    }
                    // Set the camera on the avatar.
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.Movement.Camera.LookAt(
                        Client.Self.SimPosition,
                        Client.Self.SimPosition
                    );
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                };
        }
    }
}