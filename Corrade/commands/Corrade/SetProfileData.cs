///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> setprofiledata =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    ManualResetEventSlim[] AvatarProfileDataEvent =
                    {
                        new ManualResetEventSlim(false),
                        new ManualResetEventSlim(false)
                    };
                    var properties = new Avatar.AvatarProperties();
                    var interests = new Avatar.Interests();
                    EventHandler<AvatarPropertiesReplyEventArgs> AvatarPropertiesEventHandler = (sender, args) =>
                    {
                        if (!args.AvatarID.Equals(Client.Self.AgentID))
                            return;

                        properties = args.Properties;
                        AvatarProfileDataEvent[0].Set();
                    };
                    EventHandler<AvatarInterestsReplyEventArgs> AvatarInterestsEventHandler = (sender, args) =>
                    {
                        if (!args.AvatarID.Equals(Client.Self.AgentID))
                            return;

                        interests = args.Interests;
                        AvatarProfileDataEvent[1].Set();
                    };
                    Locks.ClientInstanceAvatarsLock.EnterReadLock();
                    Client.Avatars.AvatarPropertiesReply += AvatarPropertiesEventHandler;
                    Client.Avatars.AvatarInterestsReply += AvatarInterestsEventHandler;
                    Client.Avatars.RequestAvatarProperties(Client.Self.AgentID);
                    if (
                        !WaitHandle.WaitAll(AvatarProfileDataEvent.Select(o => o.WaitHandle).ToArray(),
                            (int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Avatars.AvatarPropertiesReply -= AvatarPropertiesEventHandler;
                        Client.Avatars.AvatarInterestsReply -= AvatarInterestsEventHandler;
                        Locks.ClientInstanceAvatarsLock.ExitReadLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_PROFILE);
                    }
                    Client.Avatars.AvatarPropertiesReply -= AvatarPropertiesEventHandler;
                    Client.Avatars.AvatarInterestsReply -= AvatarInterestsEventHandler;
                    Locks.ClientInstanceAvatarsLock.ExitReadLock();
                    var fields =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                            corradeCommandParameters.Message));
                    properties = properties.wasCSVToStructure(fields, wasInput);
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client))
                    {
                        if (Encoding.UTF8.GetByteCount(properties.AboutText) >
                            wasOpenMetaverse.Constants.AVATARS.PROFILE.SECOND_LIFE_TEXT_SIZE)
                            throw new Command.ScriptException(Enumerations.ScriptError.SECOND_LIFE_TEXT_TOO_LARGE);
                        if (Encoding.UTF8.GetByteCount(properties.FirstLifeText) >
                            wasOpenMetaverse.Constants.AVATARS.PROFILE.FIRST_LIFE_TEXT_SIZE)
                            throw new Command.ScriptException(Enumerations.ScriptError.FIRST_LIFE_TEXT_TOO_LARGE);
                    }
                    interests = interests.wasCSVToStructure(fields, wasInput);
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.UpdateProfile(properties);
                    Client.Self.UpdateInterests(interests);
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                };
        }
    }
}