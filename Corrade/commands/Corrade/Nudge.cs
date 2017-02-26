///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using String = wasSharp.String;
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
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    switch (Reflection.GetEnumValueFromName<Enumerations.Direction>(
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DIRECTION)),
                            corradeCommandParameters.Message))
                        ))
                    {
                        case Enumerations.Direction.BACK:
                            lock (Locks.ClientInstanceSelfLock)
                            {
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
                            }
                            break;
                        case Enumerations.Direction.FORWARD:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.Movement.SendManualUpdate(
                                    (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                    AgentManager.ControlFlags.AGENT_CONTROL_AT_POS,
                                    Client.Self.Movement.Camera.Position,
                                    Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                    Client.Self.Movement.Camera.UpAxis,
                                    Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                    Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                    Client.Self.Movement.State, false);
                            }
                            break;
                        case Enumerations.Direction.LEFT:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.Movement.SendManualUpdate(
                                    (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                    AgentManager.ControlFlags.
                                        AGENT_CONTROL_LEFT_POS, Client.Self.Movement.Camera.Position,
                                    Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                    Client.Self.Movement.Camera.UpAxis,
                                    Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                    Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                    Client.Self.Movement.State, false);
                            }
                            break;
                        case Enumerations.Direction.RIGHT:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.Movement.SendManualUpdate(
                                    (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                    AgentManager.ControlFlags.
                                        AGENT_CONTROL_LEFT_NEG, Client.Self.Movement.Camera.Position,
                                    Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                    Client.Self.Movement.Camera.UpAxis,
                                    Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                    Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                    Client.Self.Movement.State, false);
                            }
                            break;
                        case Enumerations.Direction.UP:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.Movement.SendManualUpdate(
                                    (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                    AgentManager.ControlFlags.AGENT_CONTROL_UP_POS,
                                    Client.Self.Movement.Camera.Position,
                                    Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                    Client.Self.Movement.Camera.UpAxis,
                                    Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                    Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                    Client.Self.Movement.State, false);
                            }
                            break;
                        case Enumerations.Direction.DOWN:
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.Movement.SendManualUpdate(
                                    (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                    AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG,
                                    Client.Self.Movement.Camera.Position,
                                    Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                    Client.Self.Movement.Camera.UpAxis,
                                    Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                    Client.Self.Movement.Camera.Far, AgentFlags.None,
                                    AgentState.None, false);
                            }
                            break;
                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_DIRECTION);
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