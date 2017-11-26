///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
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
using wasSharp.Timers;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getavatarclassifieddata =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Interact))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        UUID agentUUID;
                        if (
                            !UUID.TryParse(
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AGENT)),
                                    corradeCommandParameters.Message)),
                                out agentUUID) && !Resolvers.AgentNameToUUID(Client,
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME)),
                                        corradeCommandParameters.Message)),
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                        corradeCommandParameters.Message)),
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new DecayingAlarm(corradeConfiguration.DataDecayType),
                                ref agentUUID))
                            throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);

                        UUID classifiedUUID;
                        if (
                            !UUID.TryParse(
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                                        corradeCommandParameters.Message)), out classifiedUUID))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);

                        var AvatarClassifiedsReplyEvent = new ManualResetEventSlim(false);
                        var classifieds = new Dictionary<UUID, string>();
                        EventHandler<AvatarClassifiedReplyEventArgs> AvatarClassifiedsReplyEventHandler =
                            (sender, args) =>
                            {
                                if (!args.AvatarID.Equals(agentUUID))
                                    return;

                                classifieds = args.Classifieds;
                                AvatarClassifiedsReplyEvent.Set();
                            };
                        Locks.ClientInstanceAvatarsLock.EnterReadLock();
                        Client.Avatars.AvatarClassifiedReply += AvatarClassifiedsReplyEventHandler;
                        Client.Avatars.RequestAvatarClassified(agentUUID);
                        if (!AvatarClassifiedsReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                        {
                            Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedsReplyEventHandler;
                            Locks.ClientInstanceAvatarsLock.ExitReadLock();
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_AVATAR_DATA);
                        }
                        Client.Avatars.AvatarClassifiedReply -= AvatarClassifiedsReplyEventHandler;
                        Locks.ClientInstanceAvatarsLock.ExitReadLock();

                        if (!classifieds.ContainsKey(classifiedUUID))
                            throw new Command.ScriptException(Enumerations.ScriptError.CLASSIFIED_NOT_FOUND);

                        var AvatarClassifiedInfoReplyEvent = new ManualResetEventSlim(false);
                        var profileClassified = new ClassifiedAd();
                        EventHandler<ClassifiedInfoReplyEventArgs> AvatarClassifiedInfoReplyEventHandler =
                            (sender, args) =>
                            {
                                if (!args.ClassifiedID.Equals(classifiedUUID))
                                    return;

                                profileClassified = args.Classified;
                                AvatarClassifiedInfoReplyEvent.Set();
                            };
                        Locks.ClientInstanceAvatarsLock.EnterReadLock();
                        Client.Avatars.ClassifiedInfoReply += AvatarClassifiedInfoReplyEventHandler;
                        Client.Avatars.RequestClassifiedInfo(agentUUID, classifiedUUID);
                        if (
                            !AvatarClassifiedInfoReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                        {
                            Client.Avatars.ClassifiedInfoReply -= AvatarClassifiedInfoReplyEventHandler;
                            Locks.ClientInstanceAvatarsLock.ExitReadLock();
                            throw new Command.ScriptException(
                                Enumerations.ScriptError.TIMEOUT_GETTING_PROFILE_CLASSIFIED);
                        }
                        Client.Avatars.ClassifiedInfoReply -= AvatarClassifiedInfoReplyEventHandler;
                        Locks.ClientInstanceAvatarsLock.ExitReadLock();

                        if (profileClassified.Equals(default(ClassifiedAd)))
                            throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_RETRIEVE_CLASSIFIED);

                        var data =
                            profileClassified.GetStructuredData(
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))).ToList();
                        if (data.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(data));
                    };
        }
    }
}