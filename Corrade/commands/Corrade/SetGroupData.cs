///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Timers;
using Parallel = System.Threading.Tasks.Parallel;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> setgroupdata =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Group))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    UUID groupUUID;
                    var target = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                            corradeCommandParameters.Message));
                    switch (string.IsNullOrEmpty(target))
                    {
                        case false:
                            if (!UUID.TryParse(target, out groupUUID) &&
                                !Resolvers.GroupNameToUUID(Client, target, corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout,
                                    new DecayingAlarm(corradeConfiguration.DataDecayType), ref groupUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                            break;

                        default:
                            groupUUID = corradeCommandParameters.Group.UUID;
                            break;
                    }
                    var currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                    if (!new HashSet<UUID>(currentGroups).Contains(groupUUID))
                        throw new Command.ScriptException(Enumerations.ScriptError.NOT_IN_GROUP);
                    var data =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(data))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_DATA_PROVIDED);
                    var targetGroup = new Group();
                    if (
                        !Services.RequestGroup(Client, groupUUID,
                            corradeConfiguration.ServicesTimeout,
                            ref targetGroup))
                        throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                    var gotPermissions = true;
                    Parallel.ForEach(CSV.ToKeyValue(data).Select(o => wasInput(o.Key)), (o, s) =>
                    {
                        switch (o)
                        {
                            case "Charter":
                            case "InsigniaID":
                            case "AllowPublish":
                                if (
                                    !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                        groupUUID,
                                        GroupPowers.ChangeIdentity,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        new DecayingAlarm(corradeConfiguration.DataDecayType)))
                                {
                                    gotPermissions = false;
                                    s.Break();
                                }
                                break;

                            case "MembershipFee":
                            case "OpenEnrollment":
                                if (
                                    !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                        groupUUID,
                                        GroupPowers.ChangeOptions,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        new DecayingAlarm(corradeConfiguration.DataDecayType)))
                                {
                                    gotPermissions = false;
                                    s.Break();
                                }
                                break;

                            case "ShowInList":
                                if (
                                    !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                        groupUUID,
                                        GroupPowers.FindPlaces,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        new DecayingAlarm(corradeConfiguration.DataDecayType)))
                                {
                                    gotPermissions = false;
                                    s.Break();
                                }
                                break;
                        }
                    });
                    if (!gotPermissions)
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    targetGroup = targetGroup.wasCSVToStructure(data, wasInput);
                    Client.Groups.SetGroupAcceptNotices(groupUUID,
                        targetGroup.AcceptNotices,
                        targetGroup.ListInProfile);
                    Client.Groups.UpdateGroup(groupUUID, targetGroup);
                };
        }
    }
}