///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> deletepick =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var AvatarPicksReplyEvent = new ManualResetEventSlim(false);
                    var input =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(input))
                        throw new Command.ScriptException(Enumerations.ScriptError.EMPTY_PICK_NAME);
                    var pickUUID = UUID.Zero;
                    EventHandler<AvatarPicksReplyEventArgs> AvatarPicksEventHandler = (sender, args) =>
                    {
                        if (!args.AvatarID.Equals(Client.Self.AgentID))
                            return;

                        var pick = args.Picks.AsParallel().FirstOrDefault(
                            o => string.Equals(input, o.Value, StringComparison.Ordinal));
                        if (!pick.Equals(default(KeyValuePair<UUID, string>)))
                            pickUUID = pick.Key;
                        AvatarPicksReplyEvent.Set();
                    };
                    Locks.ClientInstanceAvatarsLock.EnterReadLock();
                    Client.Avatars.AvatarPicksReply += AvatarPicksEventHandler;
                    Client.Avatars.RequestAvatarPicks(Client.Self.AgentID);
                    if (!AvatarPicksReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Avatars.AvatarPicksReply -= AvatarPicksEventHandler;
                        Locks.ClientInstanceAvatarsLock.ExitReadLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_PICKS);
                    }
                    Client.Avatars.AvatarPicksReply -= AvatarPicksEventHandler;
                    Locks.ClientInstanceAvatarsLock.ExitReadLock();
                    if (pickUUID.Equals(UUID.Zero))
                        pickUUID = UUID.Random();
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.PickDelete(pickUUID);
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                };
        }
    }
}