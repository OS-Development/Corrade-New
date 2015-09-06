using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> setprofiledata =
                (commandGroup, message, result) =>
                {
                    if (
                        !HasCorradePermission(commandGroup.Name,
                            (int) Permissions.Grooming))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    ManualResetEvent[] AvatarProfileDataEvent =
                    {
                        new ManualResetEvent(false),
                        new ManualResetEvent(false)
                    };
                    Avatar.AvatarProperties properties = new Avatar.AvatarProperties();
                    Avatar.Interests interests = new Avatar.Interests();
                    EventHandler<AvatarPropertiesReplyEventArgs> AvatarPropertiesEventHandler = (sender, args) =>
                    {
                        properties = args.Properties;
                        AvatarProfileDataEvent[0].Set();
                    };
                    EventHandler<AvatarInterestsReplyEventArgs> AvatarInterestsEventHandler = (sender, args) =>
                    {
                        interests = args.Interests;
                        AvatarProfileDataEvent[1].Set();
                    };
                    lock (ClientInstanceAvatarsLock)
                    {
                        Client.Avatars.AvatarPropertiesReply += AvatarPropertiesEventHandler;
                        Client.Avatars.AvatarInterestsReply += AvatarInterestsEventHandler;
                        Client.Avatars.RequestAvatarProperties(Client.Self.AgentID);
                        if (
                            !WaitHandle.WaitAll(AvatarProfileDataEvent.Select(o => (WaitHandle) o).ToArray(),
                                (int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Avatars.AvatarPropertiesReply -= AvatarPropertiesEventHandler;
                            Client.Avatars.AvatarInterestsReply -= AvatarInterestsEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_PROFILE);
                        }
                        Client.Avatars.AvatarPropertiesReply -= AvatarPropertiesEventHandler;
                        Client.Avatars.AvatarInterestsReply -= AvatarInterestsEventHandler;
                    }
                    string fields =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DATA)),
                            message));
                    wasCSVToStructure(fields, ref properties);
                    if (IsSecondLife())
                    {
                        if (Encoding.UTF8.GetByteCount(properties.AboutText) >
                            LINDEN_CONSTANTS.AVATARS.PROFILE.SECOND_LIFE_TEXT_SIZE)
                        {
                            throw new ScriptException(ScriptError.SECOND_LIFE_TEXT_TOO_LARGE);
                        }
                        if (Encoding.UTF8.GetByteCount(properties.FirstLifeText) >
                            LINDEN_CONSTANTS.AVATARS.PROFILE.FIRST_LIFE_TEXT_SIZE)
                        {
                            throw new ScriptException(ScriptError.FIRST_LIFE_TEXT_TOO_LARGE);
                        }
                    }
                    wasCSVToStructure(fields, ref interests);
                    Client.Self.UpdateProfile(properties);
                    Client.Self.UpdateInterests(interests);
                };
        }
    }
}