///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> startproposal =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                    }
                    if (!new HashSet<UUID>(currentGroups).Contains(corradeCommandParameters.Group.UUID))
                    {
                        throw new ScriptException(ScriptError.NOT_IN_GROUP);
                    }
                    if (
                        !Services.HasGroupPowers(Client, Client.Self.AgentID, corradeCommandParameters.Group.UUID,
                            GroupPowers.StartProposal,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    int duration;
                    if (
                        !int.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DURATION)),
                                    corradeCommandParameters.Message)),
                            out duration))
                    {
                        throw new ScriptException(ScriptError.INVALID_PROPOSAL_DURATION);
                    }
                    float majority;
                    if (
                        !float.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.MAJORITY)),
                                    corradeCommandParameters.Message)),
                            out majority))
                    {
                        throw new ScriptException(ScriptError.INVALID_PROPOSAL_MAJORITY);
                    }
                    int quorum;
                    if (
                        !int.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.QUORUM)),
                                    corradeCommandParameters.Message)),
                            out quorum))
                    {
                        throw new ScriptException(ScriptError.INVALID_PROPOSAL_QUORUM);
                    }
                    var text =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TEXT)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(text))
                    {
                        throw new ScriptException(ScriptError.INVALID_PROPOSAL_TEXT);
                    }
                    lock (Locks.ClientInstanceGroupsLock)
                    {
                        Client.Groups.StartProposal(corradeCommandParameters.Group.UUID, new GroupProposal
                        {
                            Duration = duration,
                            Majority = majority,
                            Quorum = quorum,
                            VoteText = text
                        });
                    }
                };
        }
    }
}