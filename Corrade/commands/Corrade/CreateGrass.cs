///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> creategrass =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_POSITION);
                    Quaternion rotation;
                    if (
                        !Quaternion.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ROTATION)),
                                    corradeCommandParameters.Message)),
                            out rotation))
                        rotation = Quaternion.Identity;
                    var region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                corradeCommandParameters.Message));
                    Locks.ClientInstanceNetworkLock.EnterReadLock();
                    var simulator = Client.Network.Simulators.AsParallel().FirstOrDefault(
                        o =>
                            o.Name.Equals(
                                string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                StringComparison.OrdinalIgnoreCase));
                    Locks.ClientInstanceNetworkLock.ExitReadLock();
                    if (simulator == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                    Parcel parcel = null;
                    if (
                        !Services.GetParcelAtPosition(Client, simulator, position, corradeConfiguration.ServicesTimeout,
                            corradeConfiguration.DataTimeout,
                            ref parcel))
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                    // Check if Corrade has permissions in the parcel group.
                    var initialGroup = Client.Self.ActiveGroup;
                    if (!parcel.OwnerID.Equals(Client.Self.AgentID) &&
                        !Services.HasGroupPowers(Client, Client.Self.AgentID,
                            parcel.GroupID,
                            GroupPowers.LandGardening,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    Vector3 scale;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SCALE)),
                                    corradeCommandParameters.Message)),
                            out scale))
                        scale = new Vector3(0.5f, 0.5f, 0.5f);
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                        (scale.X < wasOpenMetaverse.Constants.PRIMITIVES.MINIMUM_SIZE_X ||
                         scale.Y < wasOpenMetaverse.Constants.PRIMITIVES.MINIMUM_SIZE_Y ||
                         scale.Z < wasOpenMetaverse.Constants.PRIMITIVES.MINIMUM_SIZE_Z ||
                         scale.X > wasOpenMetaverse.Constants.PRIMITIVES.MAXIMUM_SIZE_X ||
                         scale.Y > wasOpenMetaverse.Constants.PRIMITIVES.MAXIMUM_SIZE_Y ||
                         scale.Z > wasOpenMetaverse.Constants.PRIMITIVES.MAXIMUM_SIZE_Z))
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.SCALE_WOULD_EXCEED_BUILDING_CONSTRAINTS);
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
                        throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_GRASS_TYPE);

                    // Activate parcel group.
                    Locks.ClientInstanceGroupsLock.EnterWriteLock();
                    Client.Groups.ActivateGroup(parcel.GroupID);

                    // Finally, add the grass to the simulator.
                    Locks.ClientInstanceObjectsLock.EnterWriteLock();
                    Client.Objects.AddGrass(simulator, scale, rotation, position,
                        (Grass) grassFieldInfo.GetValue(null),
                        corradeCommandParameters.Group.UUID);
                    Locks.ClientInstanceObjectsLock.ExitWriteLock();

                    // Activate the initial group.
                    Client.Groups.ActivateGroup(initialGroup);
                    Locks.ClientInstanceGroupsLock.ExitWriteLock();
                };
        }
    }
}