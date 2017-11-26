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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getavatarpicks =
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
                    var AvatarPicksReplyEvent = new ManualResetEventSlim(false);
                    var picks = new Dictionary<UUID, string>();
                    EventHandler<AvatarPicksReplyEventArgs> AvatarPicksReplyEventHandler =
                        (sender, args) =>
                        {
                            if (!args.AvatarID.Equals(agentUUID))
                                return;

                            picks = args.Picks;
                            AvatarPicksReplyEvent.Set();
                        };
                    Locks.ClientInstanceAvatarsLock.EnterReadLock();
                    Client.Avatars.AvatarPicksReply += AvatarPicksReplyEventHandler;
                    Client.Avatars.RequestAvatarPicks(agentUUID);
                    if (!AvatarPicksReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Avatars.AvatarPicksReply -= AvatarPicksReplyEventHandler;
                        Locks.ClientInstanceAvatarsLock.ExitReadLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_AVATAR_DATA);
                    }
                    Client.Avatars.AvatarPicksReply -= AvatarPicksReplyEventHandler;
                    Locks.ClientInstanceAvatarsLock.ExitReadLock();
                    if (picks.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), CSV.FromDictionary(picks));
                };
        }
    }
}