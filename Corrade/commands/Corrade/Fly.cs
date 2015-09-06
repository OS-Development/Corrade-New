///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> fly = (commandGroup, message, result) =>
            {
                if (
                    !HasCorradePermission(commandGroup.Name,
                        (int) Permissions.Movement))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                Action action =
                    wasGetEnumValueFromDescription<Action>(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)), message))
                            .ToLowerInvariant());
                switch (action)
                {
                    case Action.START:
                    case Action.STOP:
                        if (Client.Self.Movement.SitOnGround || !Client.Self.SittingOn.Equals(0))
                        {
                            Client.Self.Stand();
                        }
                        // stop all non-built-in animations
                        List<UUID> lindenAnimations = new List<UUID>(typeof (Animations).GetProperties(
                            BindingFlags.Public |
                            BindingFlags.Static).AsParallel().Select(o => (UUID) o.GetValue(null)).ToList());
                        Parallel.ForEach(Client.Self.SignaledAnimations.Copy().Keys, o =>
                        {
                            if (!lindenAnimations.Contains(o))
                                Client.Self.AnimationStop(o, true);
                        });
                        Client.Self.Fly(action.Equals(Action.START));
                        break;
                    default:
                        throw new ScriptException(ScriptError.FLY_ACTION_START_OR_STOP);
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