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
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> setgroupdata =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    IEnumerable<UUID> currentGroups = Enumerable.Empty<UUID>();
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
                    string data = wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(data))
                    {
                        throw new ScriptException(ScriptError.NO_DATA_PROVIDED);
                    }
                    Group targetGroup = new Group();
                    if (
                        !Services.RequestGroup(Client, corradeCommandParameters.Group.UUID,
                            corradeConfiguration.ServicesTimeout,
                            ref targetGroup))
                    {
                        throw new ScriptException(ScriptError.GROUP_NOT_FOUND);
                    }
                    bool gotPermissions = true;
                    Parallel.ForEach(CSV.ToKeyValue(data).Select(o => o.Key), (o, s) =>
                    {
                        switch (o)
                        {
                            case "Charter":
                            case "InsigniaID":
                            case "AllowPublish":
                                if (
                                    !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                        corradeCommandParameters.Group.UUID,
                                        GroupPowers.ChangeIdentity,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                                {
                                    gotPermissions = false;
                                    s.Break();
                                }
                                break;
                            case "MembershipFee":
                            case "OpenEnrollment":
                                if (
                                    !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                        corradeCommandParameters.Group.UUID,
                                        GroupPowers.ChangeOptions,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                                {
                                    gotPermissions = false;
                                    s.Break();
                                }
                                break;
                            case "ShowInList":
                                if (
                                    !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                        corradeCommandParameters.Group.UUID,
                                        GroupPowers.FindPlaces,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                                {
                                    gotPermissions = false;
                                    s.Break();
                                }
                                break;
                        }
                    });
                    if (!gotPermissions)
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    wasCSVToStructure(data, ref targetGroup);
                    lock (Locks.ClientInstanceGroupsLock)
                    {
                        Client.Groups.SetGroupAcceptNotices(corradeCommandParameters.Group.UUID,
                            targetGroup.AcceptNotices,
                            targetGroup.ListInProfile);
                        Client.Groups.UpdateGroup(corradeCommandParameters.Group.UUID, targetGroup);
                    }
                };
        }
    }
}