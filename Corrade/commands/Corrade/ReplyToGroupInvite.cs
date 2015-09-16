///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> replytogroupinvite =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    uint action =
                        (uint) wasGetEnumValueFromDescription<Action>(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant());
                    IEnumerable<UUID> currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !GetCurrentGroups(corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                    }
                    if (new HashSet<UUID>(currentGroups).Contains(corradeCommandParameters.Group.UUID))
                    {
                        throw new ScriptException(ScriptError.ALREADY_IN_GROUP);
                    }
                    UUID sessionUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.SESSION)),
                                    corradeCommandParameters.Message)),
                            out sessionUUID))
                    {
                        throw new ScriptException(ScriptError.NO_SESSION_SPECIFIED);
                    }
                    lock (GroupInviteLock)
                    {
                        if (!GroupInvites.AsParallel().Any(o => o.Session.Equals(sessionUUID)))
                        {
                            throw new ScriptException(ScriptError.UNKNOWN_GROUP_INVITE_SESSION);
                        }
                    }
                    int amount;
                    lock (GroupInviteLock)
                    {
                        GroupInvite groupInvite =
                            GroupInvites.AsParallel().FirstOrDefault(o => o.Session.Equals(sessionUUID));
                        switch (!groupInvite.Equals(default(GroupInvite)))
                        {
                            case true:
                                amount = groupInvite.Fee;
                                break;
                            default:
                                throw new ScriptException(ScriptError.UNKNOWN_GROUP_INVITE_SESSION);
                        }
                    }
                    if (!amount.Equals(0) && action.Equals((uint) Action.ACCEPT))
                    {
                        if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Economy))
                        {
                            throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                        }
                        if (!UpdateBalance(corradeConfiguration.ServicesTimeout))
                        {
                            throw new ScriptException(ScriptError.UNABLE_TO_OBTAIN_MONEY_BALANCE);
                        }
                        if (Client.Self.Balance < amount)
                        {
                            throw new ScriptException(ScriptError.INSUFFICIENT_FUNDS);
                        }
                    }
                    Client.Self.GroupInviteRespond(corradeCommandParameters.Group.UUID, sessionUUID,
                        action.Equals((uint) Action.ACCEPT));
                };
        }
    }
}