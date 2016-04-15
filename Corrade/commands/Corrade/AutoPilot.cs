///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;
using Helpers = wasOpenMetaverse.Helpers;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> autopilot =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Movement))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    switch (
                        Reflection.GetEnumValueFromName<Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Action.START:
                            Vector3 position;
                            if (
                                !Vector3.TryParse(
                                    wasInput(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION)),
                                        corradeCommandParameters.Message)),
                                    out position))
                            {
                                throw new ScriptException(ScriptError.INVALID_POSITION);
                            }
                            uint moveRegionX, moveRegionY;
                            Utils.LongToUInts(Client.Network.CurrentSim.Handle, out moveRegionX, out moveRegionY);
                            if (Client.Self.Movement.SitOnGround || !Client.Self.SittingOn.Equals(0))
                            {
                                Client.Self.Stand();
                            }
                            // stop non default animations if requested
                            bool deanimate;
                            switch (bool.TryParse(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DEANIMATE)),
                                    corradeCommandParameters.Message)), out deanimate) && deanimate)
                            {
                                case true:
                                    // stop all non-built-in animations
                                    Client.Self.SignaledAnimations.Copy()
                                        .Keys.AsParallel()
                                        .Where(o => !Helpers.LindenAnimations.Contains(o))
                                        .ForAll(o => { Client.Self.AnimationStop(o, true); });
                                    break;
                            }
                            Client.Self.AutoPilotCancel();
                            Client.Self.Movement.TurnToward(position, true);
                            Client.Self.AutoPilot(position.X + moveRegionX, position.Y + moveRegionY, position.Z);
                            break;
                        case Action.STOP:
                            Client.Self.AutoPilotCancel();
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_MOVE_ACTION);
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