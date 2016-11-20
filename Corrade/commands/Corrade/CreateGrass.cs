///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> creategrass =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_POSITION);
                    }
                    Quaternion rotation;
                    if (
                        !Quaternion.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ROTATION)),
                                    corradeCommandParameters.Message)),
                            out rotation))
                    {
                        rotation = Quaternion.Identity;
                    }
                    var region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                corradeCommandParameters.Message));
                    Simulator simulator;
                    lock (Locks.ClientInstanceNetworkLock)
                    {
                        simulator =
                            Client.Network.Simulators.AsParallel().FirstOrDefault(
                                o =>
                                    o.Name.Equals(
                                        string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                        StringComparison.OrdinalIgnoreCase));
                    }
                    if (simulator == null)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                    }
                    Parcel parcel = null;
                    if (
                        !Services.GetParcelAtPosition(Client, simulator, position, corradeConfiguration.ServicesTimeout,
                            ref parcel))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                    }
                    if (!parcel.OwnerID.Equals(Client.Self.AgentID))
                    {
                        if (!parcel.IsGroupOwned && !parcel.GroupID.Equals(corradeCommandParameters.Group.UUID))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                        }
                        if (
                            !Services.HasGroupPowers(Client, Client.Self.AgentID, corradeCommandParameters.Group.UUID,
                                GroupPowers.LandGardening,
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                        }
                    }
                    Vector3 scale;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SCALE)),
                                    corradeCommandParameters.Message)),
                            out scale))
                    {
                        scale = new Vector3(0.5f, 0.5f, 0.5f);
                    }
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                        (scale.X < wasOpenMetaverse.Constants.PRIMITIVES.MINIMUM_SIZE_X ||
                         scale.Y < wasOpenMetaverse.Constants.PRIMITIVES.MINIMUM_SIZE_Y ||
                         scale.Z < wasOpenMetaverse.Constants.PRIMITIVES.MINIMUM_SIZE_Z ||
                         scale.X > wasOpenMetaverse.Constants.PRIMITIVES.MAXIMUM_SIZE_X ||
                         scale.Y > wasOpenMetaverse.Constants.PRIMITIVES.MAXIMUM_SIZE_Y ||
                         scale.Z > wasOpenMetaverse.Constants.PRIMITIVES.MAXIMUM_SIZE_Z))
                    {
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.SCALE_WOULD_EXCEED_BUILDING_CONSTRAINTS);
                    }
                    var type = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                            corradeCommandParameters.Message));
                    var grassFieldInfo = typeof(Grass).GetFields(
                        BindingFlags.Public |
                        BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(type,
                                    StringComparison.OrdinalIgnoreCase));
                    if (grassFieldInfo == null)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_GRASS_TYPE);
                    }
                    // Finally, add the grass to the simulator.
                    lock (Locks.ClientInstanceObjectsLock)
                    {
                        Client.Objects.AddGrass(simulator, scale, rotation, position,
                            (Grass) grassFieldInfo.GetValue(null),
                            corradeCommandParameters.Group.UUID);
                    }
                };
        }
    }
}