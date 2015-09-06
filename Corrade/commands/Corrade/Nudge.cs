using System;
using System.Collections.Generic;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> nudge = (commandGroup, message, result) =>
            {
                if (
                    !HasCorradePermission(commandGroup.Name,
                        (int) Permissions.Movement))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                switch (wasGetEnumValueFromDescription<Direction>(
                    wasInput(wasKeyValueGet(
                        wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DIRECTION)),
                        message))
                        .ToLowerInvariant()))
                {
                    case Direction.BACK:
                        Client.Self.Movement.SendManualUpdate(AgentManager.ControlFlags.AGENT_CONTROL_AT_NEG,
                            Client.Self.Movement.Camera.Position,
                            Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                            Client.Self.Movement.Camera.UpAxis,
                            Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                            Client.Self.Movement.Camera.Far, AgentFlags.None, AgentState.None, true);
                        break;
                    case Direction.FORWARD:
                        Client.Self.Movement.SendManualUpdate(AgentManager.ControlFlags.AGENT_CONTROL_AT_POS,
                            Client.Self.Movement.Camera.Position,
                            Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                            Client.Self.Movement.Camera.UpAxis,
                            Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                            Client.Self.Movement.Camera.Far, AgentFlags.None,
                            AgentState.None, true);
                        break;
                    case Direction.LEFT:
                        Client.Self.Movement.SendManualUpdate(AgentManager.ControlFlags.
                            AGENT_CONTROL_LEFT_POS, Client.Self.Movement.Camera.Position,
                            Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                            Client.Self.Movement.Camera.UpAxis,
                            Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                            Client.Self.Movement.Camera.Far, AgentFlags.None,
                            AgentState.None, true);
                        break;
                    case Direction.RIGHT:
                        Client.Self.Movement.SendManualUpdate(AgentManager.ControlFlags.
                            AGENT_CONTROL_LEFT_NEG, Client.Self.Movement.Camera.Position,
                            Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                            Client.Self.Movement.Camera.UpAxis,
                            Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                            Client.Self.Movement.Camera.Far, AgentFlags.None,
                            AgentState.None, true);
                        break;
                    case Direction.UP:
                        Client.Self.Movement.SendManualUpdate(AgentManager.ControlFlags.AGENT_CONTROL_UP_POS,
                            Client.Self.Movement.Camera.Position,
                            Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                            Client.Self.Movement.Camera.UpAxis,
                            Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                            Client.Self.Movement.Camera.Far, AgentFlags.None,
                            AgentState.None, true);
                        break;
                    case Direction.DOWN:
                        Client.Self.Movement.SendManualUpdate(AgentManager.ControlFlags.AGENT_CONTROL_UP_NEG,
                            Client.Self.Movement.Camera.Position,
                            Client.Self.Movement.Camera.AtAxis, Client.Self.Movement.Camera.LeftAxis,
                            Client.Self.Movement.Camera.UpAxis,
                            Client.Self.Movement.BodyRotation, Client.Self.Movement.HeadRotation,
                            Client.Self.Movement.Camera.Far, AgentFlags.None,
                            AgentState.None, true);
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