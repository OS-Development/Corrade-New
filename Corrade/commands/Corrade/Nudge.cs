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

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> nudge =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Movement))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    switch (Reflection.GetEnumValueFromName<Direction>(
                        wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DIRECTION)),
                            corradeCommandParameters.Message))
                            .ToLowerInvariant()))
                    {
                        case Direction.BACK:
                            Client.Self.Movement.SendManualUpdate(
                                (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG,
                                Client.Self.Movement.Camera.Position,
                                Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                Client.Self.Movement.Camera.UpAxis,
                                Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags, Client.Self.Movement.State, false);
                            break;
                        case Direction.FORWARD:
                            Client.Self.Movement.SendManualUpdate(
                                (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                AgentManager.ControlFlags.AGENT_CONTROL_AT_POS,
                                Client.Self.Movement.Camera.Position,
                                Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                Client.Self.Movement.Camera.UpAxis,
                                Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                Client.Self.Movement.State, false);
                            break;
                        case Direction.LEFT:
                            Client.Self.Movement.SendManualUpdate(
                                (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                AgentManager.ControlFlags.
                                    AGENT_CONTROL_LEFT_POS, Client.Self.Movement.Camera.Position,
                                Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                Client.Self.Movement.Camera.UpAxis,
                                Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                Client.Self.Movement.State, false);
                            break;
                        case Direction.RIGHT:
                            Client.Self.Movement.SendManualUpdate(
                                (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                AgentManager.ControlFlags.
                                    AGENT_CONTROL_LEFT_NEG, Client.Self.Movement.Camera.Position,
                                Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                Client.Self.Movement.Camera.UpAxis,
                                Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                Client.Self.Movement.State, false);
                            break;
                        case Direction.UP:
                            Client.Self.Movement.SendManualUpdate(
                                (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                AgentManager.ControlFlags.AGENT_CONTROL_UP_POS,
                                Client.Self.Movement.Camera.Position,
                                Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                Client.Self.Movement.Camera.UpAxis,
                                Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                Client.Self.Movement.Camera.Far, Client.Self.Movement.Flags,
                                Client.Self.Movement.State, false);
                            break;
                        case Direction.DOWN:
                            Client.Self.Movement.SendManualUpdate(
                                (AgentManager.ControlFlags) Client.Self.Movement.AgentControls |
                                AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG,
                                Client.Self.Movement.Camera.Position,
                                Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                                Client.Self.Movement.Camera.UpAxis,
                                Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                                Client.Self.Movement.Camera.Far, AgentFlags.None,
                                AgentState.None, false);
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_DIRECTION);
                    }
                    // Set the camera on the avatar.
                    Client.Self.Movement.Camera.LookAt(
                        Client.Self.SimPosition,
                        Client.Self.SimPosition
                        );
                };
        }
    }
}