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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> deleteclassified
                =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var name =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                        throw new Command.ScriptException(Enumerations.ScriptError.EMPTY_CLASSIFIED_NAME);
                    var AvatarClassifiedReplyEvent = new ManualResetEventSlim(false);
                    var classifiedUUID = UUID.Zero;
                    EventHandler<AvatarClassifiedReplyEventArgs> AvatarClassifiedEventHandler = (sender, args) =>
                    {
                        if (!args.AvatarID.Equals(Client.Self.AgentID))
                            return;

                        var classified = args.Classifieds.AsParallel().FirstOrDefault(
                            o =>
                                string.Equals(name, o.Value, StringComparison.Ordinal));
                        if (!classified.Equals(default(KeyValuePair<UUID, string>)))
                            classifiedUUID = classified.Key;
                        AvatarClassifiedReplyEvent.Set();
                    };
                    Locks.ClientInstanceAvatarsLock.EnterReadLock();
                    Client.Avatars.AvatarClassifiedReply += AvatarClassifiedEventHandler;
                    Client.Avatars.RequestAvatarClassified(Client.Self.AgentID);
                    if (!AvatarClassifiedReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedEventHandler;
                        Locks.ClientInstanceAvatarsLock.ExitReadLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_CLASSIFIEDS);
                    }
                    Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedEventHandler;
                    Locks.ClientInstanceAvatarsLock.ExitReadLock();
                    if (classifiedUUID.Equals(UUID.Zero))
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_CLASSIFIED);
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.DeleteClassfied(classifiedUUID);
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                };
        }
    }
}