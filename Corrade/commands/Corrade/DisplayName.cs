///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Threading;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> displayname =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Grooming))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string previous = string.Empty;
                    Client.Avatars.GetDisplayNames(new List<UUID> {Client.Self.AgentID},
                        (succeded, names, IDs) =>
                        {
                            if (!succeded || names.Length < 1)
                            {
                                throw new ScriptException(ScriptError.FAILED_TO_GET_DISPLAY_NAME);
                            }
                            previous = names[0].DisplayName;
                        });
                    switch (
                        wasGetEnumValueFromDescription<Action>(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)),
                                    message)).ToLowerInvariant()))
                    {
                        case Action.GET:
                            result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA), previous);
                            break;
                        case Action.SET:
                            string name =
                                wasInput(
                                    wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.NAME)),
                                        message));
                            if (string.IsNullOrEmpty(name))
                            {
                                throw new ScriptException(ScriptError.NO_NAME_PROVIDED);
                            }
                            if (IsSecondLife() &&
                                (name.Length > LINDEN_CONSTANTS.AVATARS.MAXIMUM_DISPLAY_NAME_CHARACTERS ||
                                 name.Length < LINDEN_CONSTANTS.AVATARS.MINIMUM_DISPLAY_NAME_CHARACTERS))
                            {
                                throw new Exception(
                                    wasGetDescriptionFromEnumValue(
                                        ScriptError.TOO_MANY_OR_TOO_FEW_CHARACTERS_FOR_DISPLAY_NAME));
                            }
                            bool succeeded = true;
                            ManualResetEvent SetDisplayNameEvent = new ManualResetEvent(false);
                            EventHandler<SetDisplayNameReplyEventArgs> SetDisplayNameEventHandler =
                                (sender, args) =>
                                {
                                    succeeded = args.Status.Equals(LINDEN_CONSTANTS.AVATARS.SET_DISPLAY_NAME_SUCCESS);
                                    SetDisplayNameEvent.Set();
                                };
                            lock (ClientInstanceSelfLock)
                            {
                                Client.Self.SetDisplayNameReply += SetDisplayNameEventHandler;
                                Client.Self.SetDisplayName(previous, name);
                                if (!SetDisplayNameEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    Client.Self.SetDisplayNameReply -= SetDisplayNameEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_WAITING_FOR_ESTATE_LIST);
                                }
                                Client.Self.SetDisplayNameReply -= SetDisplayNameEventHandler;
                            }
                            if (!succeeded)
                            {
                                throw new ScriptException(ScriptError.COULD_NOT_SET_DISPLAY_NAME);
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}