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
            public static Action<Group, string, Dictionary<string, string>> invite = (commandGroup, message, result) =>
            {
                if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Group))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                if (
                    !HasGroupPowers(Client.Self.AgentID, commandGroup.UUID, GroupPowers.Invite,
                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                {
                    throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                }
                UUID agentUUID;
                if (
                    !UUID.TryParse(
                        wasInput(wasKeyValueGet(
                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.AGENT)), message)),
                        out agentUUID) && !AgentNameToUUID(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.FIRSTNAME)),
                                    message)),
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.LASTNAME)),
                                    message)),
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            ref agentUUID))
                {
                    throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                }
                if (AgentInGroup(agentUUID, commandGroup.UUID, corradeConfiguration.ServicesTimeout))
                {
                    throw new ScriptException(ScriptError.ALREADY_IN_GROUP);
                }
                HashSet<UUID> roleUUIDs = new HashSet<UUID>();
                foreach (
                    string role in
                        wasCSVToEnumerable(
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ROLE)),
                                message)))
                            .AsParallel().Where(o => !string.IsNullOrEmpty(o)))
                {
                    UUID roleUUID;
                    if (!UUID.TryParse(role, out roleUUID) &&
                        !RoleNameToUUID(role, commandGroup.UUID,
                            corradeConfiguration.ServicesTimeout, ref roleUUID))
                    {
                        throw new ScriptException(ScriptError.ROLE_NOT_FOUND);
                    }
                    if (!roleUUIDs.Contains(roleUUID))
                    {
                        roleUUIDs.Add(roleUUID);
                    }
                }
                // No roles specified, so assume everyone role.
                if (!roleUUIDs.Any())
                {
                    roleUUIDs.Add(UUID.Zero);
                }
                if (!roleUUIDs.All(o => o.Equals(UUID.Zero)) &&
                    !HasGroupPowers(Client.Self.AgentID, commandGroup.UUID, GroupPowers.AssignMember,
                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                {
                    throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                }
                Client.Groups.Invite(commandGroup.UUID, roleUUIDs.ToList(), agentUUID);
            };
        }
    }
}