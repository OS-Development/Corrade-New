///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> displayname =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Grooming))
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
                        Reflection.GetEnumValueFromName<Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Action.GET:
                            result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA), previous);
                            break;
                        case Action.SET:
                            string name =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.NAME)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(name))
                            {
                                throw new ScriptException(ScriptError.NO_NAME_PROVIDED);
                            }
                            if (IsSecondLife() &&
                                (name.Length > Constants.AVATARS.MAXIMUM_DISPLAY_NAME_CHARACTERS ||
                                 name.Length < Constants.AVATARS.MINIMUM_DISPLAY_NAME_CHARACTERS))
                            {
                                throw new Exception(
                                    Reflection.GetNameFromEnumValue(
                                        ScriptError.TOO_MANY_OR_TOO_FEW_CHARACTERS_FOR_DISPLAY_NAME));
                            }
                            bool succeeded = true;
                            ManualResetEvent SetDisplayNameEvent = new ManualResetEvent(false);
                            EventHandler<SetDisplayNameReplyEventArgs> SetDisplayNameEventHandler =
                                (sender, args) =>
                                {
                                    succeeded =
                                        args.Status.Equals((int) Constants.AVATARS.SET_DISPLAY_NAME_SUCCESS);
                                    SetDisplayNameEvent.Set();
                                };
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.SetDisplayNameReply += SetDisplayNameEventHandler;
                                Client.Self.SetDisplayName(previous, name);
                                if (!SetDisplayNameEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    Client.Self.SetDisplayNameReply -= SetDisplayNameEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_WAITING_FOR_DISPLAY_NAME);
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