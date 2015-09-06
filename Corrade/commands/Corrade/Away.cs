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
            public static Action<Group, string, Dictionary<string, string>> away = (commandGroup, message, result) =>
            {
                if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Grooming))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                switch (wasGetEnumValueFromDescription<Action>(
                    wasInput(
                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)), message))
                        .ToLowerInvariant()))
                {
                    case Action.ENABLE:
                        Client.Self.AnimationStart(Animations.AWAY, true);
                        Client.Self.Movement.Away = true;
                        Client.Self.Movement.SendUpdate(true);
                        break;
                    case Action.DISABLE:
                        Client.Self.Movement.Away = false;
                        Client.Self.AnimationStop(Animations.AWAY, true);
                        Client.Self.Movement.SendUpdate(true);
                        break;
                    case Action.GET:
                        result.Add(wasGetDescriptionFromEnumValue(ScriptKeys.DATA),
                            Client.Self.Movement.Away.ToString());
                        break;
                    default:
                        throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                }
            };
        }
    }
}