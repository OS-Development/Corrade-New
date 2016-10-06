///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Structures;
using CorradeConfiguration;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                replytogroupinvite =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Group))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        }
                        var action =
                            (uint) Reflection.GetEnumValueFromName<Enumerations.Action>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                        corradeCommandParameters.Message))
                                    .ToLowerInvariant());
                        var currentGroups = Enumerable.Empty<UUID>();
                        if (
                            !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                                ref currentGroups))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                        }
                        if (new HashSet<UUID>(currentGroups).Contains(corradeCommandParameters.Group.UUID))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.ALREADY_IN_GROUP);
                        }
                        UUID sessionUUID;
                        if (
                            !UUID.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SESSION)),
                                        corradeCommandParameters.Message)),
                                out sessionUUID))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_SESSION_SPECIFIED);
                        }
                        GroupInvite groupInvite;
                        int amount;
                        lock (GroupInviteLock)
                        {
                            groupInvite =
                                GroupInvites.AsParallel().FirstOrDefault(o => o.Session.Equals(sessionUUID));
                            switch (!groupInvite.Equals(default(GroupInvite)))
                            {
                                case true:
                                    amount = groupInvite.Fee;
                                    break;
                                default:
                                    throw new Command.ScriptException(Enumerations.ScriptError.GROUP_INVITE_NOT_FOUND);
                            }
                        }
                        if (!amount.Equals(0) && action.Equals((uint) Enumerations.Action.ACCEPT))
                        {
                            if (
                                !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                    (int) Configuration.Permissions.Economy))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                            }
                            if (!Services.UpdateBalance(Client, corradeConfiguration.ServicesTimeout))
                            {
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.UNABLE_TO_OBTAIN_MONEY_BALANCE);
                            }
                            if (Client.Self.Balance < amount)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.INSUFFICIENT_FUNDS);
                            }
                        }
                        // remove the group invite
                        lock (GroupInviteLock)
                        {
                            GroupInvites.Remove(groupInvite);
                        }
                        lock (Locks.ClientInstanceSelfLock)
                        {
                            Client.Self.GroupInviteRespond(corradeCommandParameters.Group.UUID, sessionUUID,
                                action.Equals((uint) Enumerations.Action.ACCEPT));
                        }
                    };
        }
    }
}