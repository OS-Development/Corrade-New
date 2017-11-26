///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> startproposal =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Group))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                    if (!new HashSet<UUID>(currentGroups).Contains(corradeCommandParameters.Group.UUID))
                        throw new Command.ScriptException(Enumerations.ScriptError.NOT_IN_GROUP);
                    if (
                        !Services.HasGroupPowers(Client, Client.Self.AgentID, corradeCommandParameters.Group.UUID,
                            GroupPowers.StartProposal,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    uint duration;
                    if (
                        !uint.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DURATION)),
                                    corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture,
                            out duration))
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_PROPOSAL_DURATION);
                    float majority;
                    if (
                        !float.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MAJORITY)),
                                    corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                            out majority))
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_PROPOSAL_MAJORITY);
                    uint quorum;
                    if (
                        !uint.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.QUORUM)),
                                    corradeCommandParameters.Message)), NumberStyles.Integer, Utils.EnUsCulture,
                            out quorum))
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_PROPOSAL_QUORUM);
                    var text =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TEXT)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(text))
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_PROPOSAL_TEXT);
                    Client.Groups.StartProposal(corradeCommandParameters.Group.UUID, new GroupProposal
                    {
                        Duration = (int) duration,
                        Majority = majority,
                        Quorum = (int) quorum,
                        VoteText = text
                    });
                };
        }
    }
}