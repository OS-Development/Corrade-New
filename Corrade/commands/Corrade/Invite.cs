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
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> invite =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Group))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    if (
                        !Services.HasGroupPowers(Client, Client.Self.AgentID, corradeCommandParameters.Group.UUID,
                            GroupPowers.Invite,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    UUID agentUUID;
                    if (
                        !UUID.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT)),
                                corradeCommandParameters.Message)),
                            out agentUUID) && !Resolvers.AgentNameToUUID(Client,
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME)),
                                        corradeCommandParameters.Message)),
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME)),
                                        corradeCommandParameters.Message)),
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType),
                                ref agentUUID))
                    {
                        throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                    }
                    if (Services.AgentInGroup(Client, agentUUID, corradeCommandParameters.Group.UUID,
                        corradeConfiguration.ServicesTimeout))
                    {
                        throw new ScriptException(ScriptError.ALREADY_IN_GROUP);
                    }
                    HashSet<UUID> roleUUIDs = new HashSet<UUID>();
                    object LockObject = new object();
                    bool rolesFound = true;
                    Parallel.ForEach(CSV.ToEnumerable(
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ROLE)),
                            corradeCommandParameters.Message)))
                        .ToArray().AsParallel().Where(o => !string.IsNullOrEmpty(o)), (o, s) =>
                        {
                            UUID roleUUID;
                            if (!UUID.TryParse(o, out roleUUID) &&
                                !Resolvers.RoleNameToUUID(Client, o, corradeCommandParameters.Group.UUID,
                                    corradeConfiguration.ServicesTimeout, ref roleUUID))
                            {
                                rolesFound = false;
                                s.Break();
                            }
                            lock (LockObject)
                            {
                                if (!roleUUIDs.Contains(roleUUID))
                                {
                                    roleUUIDs.Add(roleUUID);
                                }
                            }
                        });
                    if (!rolesFound)
                        throw new ScriptException(ScriptError.ROLE_NOT_FOUND);
                    // No roles specified, so assume everyone role.
                    if (!roleUUIDs.Any())
                    {
                        roleUUIDs.Add(UUID.Zero);
                    }
                    if (!roleUUIDs.All(o => o.Equals(UUID.Zero)) &&
                        !Services.HasGroupPowers(Client, Client.Self.AgentID, corradeCommandParameters.Group.UUID,
                            GroupPowers.AssignMember,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    Client.Groups.Invite(corradeCommandParameters.Group.UUID, roleUUIDs.ToList(), agentUUID);
                };
        }
    }
}