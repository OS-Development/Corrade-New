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
using Helpers = wasOpenMetaverse.Helpers;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> createprimitive =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
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
                        throw new ScriptException(ScriptError.INVALID_POSITION);
                    }
                    if (Helpers.IsSecondLife(Client) &&
                        position.Z > Constants.PRIMITIVES.MAXIMUM_REZ_HEIGHT)
                    {
                        throw new ScriptException(ScriptError.POSITION_WOULD_EXCEED_MAXIMUM_REZ_ALTITUDE);
                    }
                    Quaternion rotation;
                    if (
                        !Quaternion.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ROTATION)),
                                    corradeCommandParameters.Message)),
                            out rotation))
                    {
                        rotation = Quaternion.Identity;
                    }
                    var region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.REGION)),
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
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    }
                    Parcel parcel = null;
                    if (
                        !Services.GetParcelAtPosition(Client, simulator, position, corradeConfiguration.ServicesTimeout,
                            ref parcel))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_FIND_PARCEL);
                    }
                    if (((uint) parcel.Flags & (uint) ParcelFlags.CreateObjects).Equals(0))
                    {
                        if (!simulator.IsEstateManager)
                        {
                            if (!parcel.OwnerID.Equals(Client.Self.AgentID))
                            {
                                if (!parcel.IsGroupOwned && !parcel.GroupID.Equals(corradeCommandParameters.Group.UUID))
                                {
                                    throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                                }
                                if (
                                    !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                        corradeCommandParameters.Group.UUID,
                                        GroupPowers.AllowRez,
                                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                        new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                                {
                                    throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                                }
                            }
                        }
                    }
                    Vector3 scale;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.SCALE)),
                                    corradeCommandParameters.Message)),
                            out scale))
                    {
                        scale = new Vector3(0.5f, 0.5f, 0.5f);
                    }
                    if (Helpers.IsSecondLife(Client) &&
                        (scale.X < Constants.PRIMITIVES.MINIMUM_SIZE_X ||
                         scale.Y < Constants.PRIMITIVES.MINIMUM_SIZE_Y ||
                         scale.Z < Constants.PRIMITIVES.MINIMUM_SIZE_Z ||
                         scale.X > Constants.PRIMITIVES.MAXIMUM_SIZE_X ||
                         scale.Y > Constants.PRIMITIVES.MAXIMUM_SIZE_Y ||
                         scale.Z > Constants.PRIMITIVES.MAXIMUM_SIZE_Z))
                    {
                        throw new ScriptException(ScriptError.SCALE_WOULD_EXCEED_BUILDING_CONSTRAINTS);
                    }
                    // build the primitive shape from presets by supplying "type" (or not)...
                    var primitiveShapesFieldInfo = typeof (CORRADE_CONSTANTS.PRIMTIVE_BODIES).GetFields(
                        BindingFlags.Public |
                        BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                                            corradeCommandParameters.Message)),
                                    StringComparison.OrdinalIgnoreCase));
                    Primitive.ConstructionData constructionData;
                    switch (primitiveShapesFieldInfo != null)
                    {
                        case true:
                            constructionData = (Primitive.ConstructionData) primitiveShapesFieldInfo.GetValue(null);
                            break;
                        default:
                            // Build the construction data as a default primitive box.
                            constructionData = CORRADE_CONSTANTS.PRIMTIVE_BODIES.CUBE;
                            break;
                    }
                    // ... and overwrite with manual data settings.
                    wasCSVToStructure(
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                            corradeCommandParameters.Message)),
                        ref constructionData);
                    // Get any primitive flags.
                    uint primFlags = 0;
                    CSV.ToEnumerable(
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FLAGS)),
                                corradeCommandParameters.Message)))
                        .ToArray()
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o))
                        .ForAll(
                            o =>
                                typeof (PrimFlags).GetFields(BindingFlags.Public | BindingFlags.Static)
                                    .AsParallel().Where(p => string.Equals(o, p.Name, StringComparison.Ordinal)).ForAll(
                                        q => { primFlags |= (uint) q.GetValue(null); }));

                    // Finally, add the primitive to the simulator.
                    lock (Locks.ClientInstanceObjectsLock)
                    {
                        Client.Objects.AddPrim(simulator, constructionData, corradeCommandParameters.Group.UUID,
                            position,
                            scale, rotation,
                            (PrimFlags) primFlags);
                    }
                };
        }
    }
}