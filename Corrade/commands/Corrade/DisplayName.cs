///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Threading;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> displayname =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var previous = string.Empty;
                    lock (Locks.ClientInstanceAvatarsLock)
                    {
                        Client.Avatars.GetDisplayNames(new List<UUID> {Client.Self.AgentID},
                            (succeded, names, IDs) =>
                            {
                                if (!succeded || names.Length < 1)
                                {
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.FAILED_TO_GET_DISPLAY_NAME);
                                }
                                previous = names[0].DisplayName;
                            });
                    }
                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Enumerations.Action.GET:
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), previous);
                            break;
                        case Enumerations.Action.SET:
                            var name =
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(name))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_NAME_PROVIDED);
                            }
                            if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                                (name.Length > wasOpenMetaverse.Constants.AVATARS.MAXIMUM_DISPLAY_NAME_CHARACTERS ||
                                 name.Length < wasOpenMetaverse.Constants.AVATARS.MINIMUM_DISPLAY_NAME_CHARACTERS))
                            {
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TOO_MANY_OR_TOO_FEW_CHARACTERS_FOR_DISPLAY_NAME);
                            }
                            var succeeded = true;
                            var SetDisplayNameEvent = new ManualResetEvent(false);
                            EventHandler<SetDisplayNameReplyEventArgs> SetDisplayNameEventHandler =
                                (sender, args) =>
                                {
                                    succeeded =
                                        args.Status.Equals(
                                            (int) wasOpenMetaverse.Constants.AVATARS.SET_DISPLAY_NAME_SUCCESS);
                                    SetDisplayNameEvent.Set();
                                };
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.SetDisplayNameReply += SetDisplayNameEventHandler;
                                Client.Self.SetDisplayName(previous, name);
                                if (!SetDisplayNameEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    Client.Self.SetDisplayNameReply -= SetDisplayNameEventHandler;
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.TIMEOUT_WAITING_FOR_DISPLAY_NAME);
                                }
                                Client.Self.SetDisplayNameReply -= SetDisplayNameEventHandler;
                            }
                            if (!succeeded)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_SET_DISPLAY_NAME);
                            }
                            break;
                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}