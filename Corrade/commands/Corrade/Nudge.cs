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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> nudge =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Movement))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    switch (Reflection.GetEnumValueFromName<Enumerations.Direction>(
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DIRECTION)),
                            corradeCommandParameters.Message))
                    ))
                    {
                        case Enumerations.Direction.BACK:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.Movement.SendManualUpdate(
                                (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG,
                                Client.Self.Movement.Camera.Position,
                                Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                Client.Self.Movement.Camera.UpAxis,
                                Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                Client.Self.Movement.State,
                                false);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        case Enumerations.Direction.FORWARD:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.Movement.SendManualUpdate(
                                (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                AgentManager.ControlFlags.AGENT_CONTROL_AT_POS,
                                Client.Self.Movement.Camera.Position,
                                Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                Client.Self.Movement.Camera.UpAxis,
                                Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                Client.Self.Movement.State, false);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        case Enumerations.Direction.LEFT:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.Movement.SendManualUpdate(
                                (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                AgentManager.ControlFlags.AGENT_CONTROL_LEFT_POS, Client.Self.Movement.Camera.Position,
                                Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                Client.Self.Movement.Camera.UpAxis,
                                Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                Client.Self.Movement.State, false);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        case Enumerations.Direction.RIGHT:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.Movement.SendManualUpdate(
                                (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                AgentManager.ControlFlags.AGENT_CONTROL_LEFT_NEG, Client.Self.Movement.Camera.Position,
                                Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                Client.Self.Movement.Camera.UpAxis,
                                Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                Client.Self.Movement.State, false);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        case Enumerations.Direction.UP:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.Movement.SendManualUpdate(
                                (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                AgentManager.ControlFlags.AGENT_CONTROL_UP_POS,
                                Client.Self.Movement.Camera.Position,
                                Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                Client.Self.Movement.Camera.UpAxis,
                                Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                Client.Self.Movement.State, false);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        case Enumerations.Direction.DOWN:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.Movement.SendManualUpdate(
                                (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG,
                                Client.Self.Movement.Camera.Position,
                                Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                Client.Self.Movement.Camera.UpAxis,
                                Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                Client.Self.Movement.Camera.Far, AgentFlags.None,
                                AgentState.None, false);
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