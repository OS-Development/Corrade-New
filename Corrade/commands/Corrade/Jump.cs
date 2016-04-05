///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> jump =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Movement))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Action action =
                        Reflection.GetEnumValueFromName<Action>(wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant());
                    switch (action)
                    {
                        case Action.START:
                        case Action.STOP:
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
                                    HashSet<UUID> lindenAnimations = new HashSet<UUID>(typeof (Animations).GetFields(
                                        BindingFlags.Public |
                                        BindingFlags.Static).AsParallel().Select(o => (UUID) o.GetValue(null)));
                                    Client.Self.SignaledAnimations.Copy()
                                        .Keys.ToArray().AsParallel()
                                        .Where(o => !lindenAnimations.Contains(o))
                                        .ForAll(o => { Client.Self.AnimationStop(o, true); });
                                    break;
                            }
                            Client.Self.Jump(action.Equals(Action.START));
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