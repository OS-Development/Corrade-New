///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> turnto = (commandGroup, message, result) =>
            {
                if (
                    !HasCorradePermission(commandGroup.Name,
                        (int) Permissions.Movement))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                Vector3 position;
                if (
                    !Vector3.TryParse(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION)),
                                message)),
                        out position))
                {
                    throw new ScriptException(ScriptError.INVALID_POSITION);
                }
                Client.Self.Movement.TurnToward(position, true);
                // Set the camera on the avatar.
                Client.Self.Movement.Camera.LookAt(
                    Client.Self.SimPosition,
                    Client.Self.SimPosition
                    );
            };
        }
    }
}