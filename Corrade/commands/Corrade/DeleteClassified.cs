///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> deleteclassified =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Grooming))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string name =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.NAME)),
                            message));
                    if (string.IsNullOrEmpty(name))
                    {
                        throw new ScriptException(ScriptError.EMPTY_CLASSIFIED_NAME);
                    }
                    ManualResetEvent AvatarClassifiedReplyEvent = new ManualResetEvent(false);
                    UUID classifiedUUID = UUID.Zero;
                    EventHandler<AvatarClassifiedReplyEventArgs> AvatarClassifiedEventHandler = (sender, args) =>
                    {
                        KeyValuePair<UUID, string> classified = args.Classifieds.AsParallel().FirstOrDefault(
                            o =>
                                o.Value.Equals(name, StringComparison.Ordinal));
                        if (!classified.Equals(default(KeyValuePair<UUID, string>)))
                            classifiedUUID = classified.Key;
                        AvatarClassifiedReplyEvent.Set();
                    };
                    lock (ClientInstanceAvatarsLock)
                    {
                        Client.Avatars.AvatarClassifiedReply += AvatarClassifiedEventHandler;
                        Client.Avatars.RequestAvatarClassified(Client.Self.AgentID);
                        if (!AvatarClassifiedReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_CLASSIFIEDS);
                        }
                        Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedEventHandler;
                    }
                    if (classifiedUUID.Equals(UUID.Zero))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_FIND_CLASSIFIED);
                    }
                    Client.Self.DeleteClassfied(classifiedUUID);
                };
        }
    }
}