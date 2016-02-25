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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getavatarpositions =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                    {
                        position = Client.Self.SimPosition;
                    }
                    Entity entity = Reflection.GetEnumValueFromName<Entity>(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant());
                    string region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.REGION)),
                                corradeCommandParameters.Message));
                    Simulator simulator =
                        Client.Network.Simulators.AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                    StringComparison.OrdinalIgnoreCase));
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
                                !Services.GetParcelAtPosition(Client, simulator, position,
                                    corradeConfiguration.ServicesTimeout, ref parcel))
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
                    object LockObject = new object();
                    Parallel.ForEach(avatarPositions, p =>
                    {
                        string name = string.Empty;
                        if (
                            !Resolvers.AgentUUIDToName(Client, p.Key, corradeConfiguration.ServicesTimeout,
                                ref name))
                            return;
                        switch (entity)
                        {
                            case Entity.REGION:
                                break;
                            case Entity.PARCEL:
                                Parcel avatarParcel = null;
                                if (
                                    !Services.GetParcelAtPosition(Client, simulator, p.Value,
                                        corradeConfiguration.ServicesTimeout, ref avatarParcel))
                                    return;
                                if (!avatarParcel.LocalID.Equals(parcel.LocalID)) return;
                                break;
                        }
                        lock (LockObject)
                        {
                            csv.Add(name);
                            csv.Add(p.Key.ToString());
                            csv.Add(p.Value.ToString());
                        }
                    });
                    if (csv.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                    }
                };
        }
    }
}