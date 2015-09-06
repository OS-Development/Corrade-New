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
            public static Action<Group, string, Dictionary<string, string>> getavatarpositions =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.POSITION)),
                                    message)),
                            out position))
                    {
                        position = Client.Self.SimPosition;
                    }
                    Entity entity = wasGetEnumValueFromDescription<Entity>(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ENTITY)), message))
                            .ToLowerInvariant());
                    string region =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REGION)),
                            message));
                    Simulator simulator =
                        Client.Network.Simulators.FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                    StringComparison.InvariantCultureIgnoreCase));
                    if (simulator == null)
                    {
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    }
                    Parcel parcel = null;
                    switch (entity)
                    {
                        case Entity.REGION:
                            break;
                        case Entity.PARCEL:
                            if (
                                !GetParcelAtPosition(simulator, position, ref parcel))
                            {
                                throw new ScriptException(ScriptError.COULD_NOT_FIND_PARCEL);
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                    }
                    List<string> csv = new List<string>();
                    Dictionary<UUID, Vector3> avatarPositions = new Dictionary<UUID, Vector3>();
                    simulator.AvatarPositions.ForEach(o => avatarPositions.Add(o.Key, o.Value));
                    foreach (KeyValuePair<UUID, Vector3> p in avatarPositions)
                    {
                        string name = string.Empty;
                        if (
                            !AgentUUIDToName(p.Key, corradeConfiguration.ServicesTimeout,
                                ref name))
                            continue;
                        switch (entity)
                        {
                            case Entity.REGION:
                                break;
                            case Entity.PARCEL:
                                if (parcel == null) return;
                                Parcel avatarParcel = null;
                                if (!GetParcelAtPosition(simulator, p.Value, ref avatarParcel))
                                    continue;
                                if (!avatarParcel.LocalID.Equals(parcel.LocalID)) continue;
                                break;
                        }
                        csv.Add(name);
                        csv.Add(p.Key.ToString());
                        csv.Add(p.Value.ToString());
                    }
                    if (csv.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(csv));
                    }
                };
        }
    }
}