///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Structures;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                replytogroupinvite =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Group))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        var sessionUUID = UUID.Zero;
                        GroupInvite groupInvite = null;
                        var action = Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))
                        );
                        switch (action)
                        {
                            case Enumerations.Action.ACCEPT:
                            case Enumerations.Action.DECLINE:
                            case Enumerations.Action.IGNORE:
                                if (
                                    !UUID.TryParse(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SESSION)),
                                                corradeCommandParameters.Message)),
                                        out sessionUUID))
                                    throw new Command.ScriptException(Enumerations.ScriptError.NO_SESSION_SPECIFIED);
                                lock (GroupInvitesLock)
                                {
                                    if (!GroupInvites.TryGetValue(sessionUUID, out groupInvite))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.GROUP_INVITE_NOT_FOUND);
                                }
                                break;
                        }

                        switch (action)
                        {
                            case Enumerations.Action.ACCEPT:
                                var currentGroups = Enumerable.Empty<UUID>();
                                if (
                                    !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                                        ref currentGroups))
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                                if (new HashSet<UUID>(currentGroups).Contains(corradeCommandParameters.Group.UUID))
                                    throw new Command.ScriptException(Enumerations.ScriptError.ALREADY_IN_GROUP);
                                if (!groupInvite.Fee.Equals(0))
                                {
                                    if (
                                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                            (int) Configuration.Permissions.Economy))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                                    if (!Services.UpdateBalance(Client, corradeConfiguration.ServicesTimeout))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.UNABLE_TO_OBTAIN_MONEY_BALANCE);
                                    if (Client.Self.Balance < groupInvite.Fee)
                                        throw new Command.ScriptException(Enumerations.ScriptError.INSUFFICIENT_FUNDS);
                                }
                                lock (GroupInvitesLock)
                                {
                                    GroupInvites.Remove(sessionUUID);
                                }
                                Locks.ClientInstanceSelfLock.EnterWriteLock();
                                Client.Self.GroupInviteRespond(groupInvite.ID, sessionUUID,
                                    true);
                                Locks.ClientInstanceSelfLock.ExitWriteLock();
                                break;

                            case Enumerations.Action.DECLINE:
                                lock (GroupInvitesLock)
                                {
                                    GroupInvites.Remove(sessionUUID);
                                }
                                Locks.ClientInstanceSelfLock.EnterWriteLock();
                                Client.Self.GroupInviteRespond(groupInvite.ID, sessionUUID,
                                    false);
                                Locks.ClientInstanceSelfLock.ExitWriteLock();
                                break;

                            case Enumerations.Action.PURGE:
                                lock (GroupInvitesLock)
                                {
                                    GroupInvites.Clear();
                                }
                                break;

                            case Enumerations.Action.IGNORE:
                                lock (GroupInvitesLock)
                                {
                                    GroupInvites.Remove(sessionUUID);
                                }
                                break;

                            default:
                                throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                        }
                    };
        }
    }
}