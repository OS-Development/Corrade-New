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
            public static Action<Group, string, Dictionary<string, string>> startproposal =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    IEnumerable<UUID> currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !GetCurrentGroups(corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                    }
                    if (!new HashSet<UUID>(currentGroups).Contains(commandGroup.UUID))
                    {
                        throw new ScriptException(ScriptError.NOT_IN_GROUP);
                    }
                    if (
                        !HasGroupPowers(Client.Self.AgentID, commandGroup.UUID, GroupPowers.StartProposal,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    int duration;
                    if (
                        !int.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DURATION)),
                                    message)),
                            out duration))
                    {
                        throw new ScriptException(ScriptError.INVALID_PROPOSAL_DURATION);
                    }
                    float majority;
                    if (
                        !float.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.MAJORITY)),
                                    message)),
                            out majority))
                    {
                        throw new ScriptException(ScriptError.INVALID_PROPOSAL_MAJORITY);
                    }
                    int quorum;
                    if (
                        !int.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.QUORUM)), message)),
                            out quorum))
                    {
                        throw new ScriptException(ScriptError.INVALID_PROPOSAL_QUORUM);
                    }
                    string text =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TEXT)),
                            message));
                    if (string.IsNullOrEmpty(text))
                    {
                        throw new ScriptException(ScriptError.INVALID_PROPOSAL_TEXT);
                    }
                    Client.Groups.StartProposal(commandGroup.UUID, new GroupProposal
                    {
                        Duration = duration,
                        Majority = majority,
                        Quorum = quorum,
                        VoteText = text
                    });
                };
        }
    }
}