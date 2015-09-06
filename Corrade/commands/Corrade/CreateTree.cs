///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> createtree =
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
                        throw new ScriptException(ScriptError.INVALID_POSITION);
                    }
                    if (IsSecondLife() &&
                        position.Z > LINDEN_CONSTANTS.PRIMITIVES.MAXIMUM_REZ_HEIGHT)
                    {
                        throw new Exception(
                            wasGetDescriptionFromEnumValue(
                                ScriptError.POSITION_WOULD_EXCEED_MAXIMUM_REZ_ALTITUDE));
                    }
                    Quaternion rotation;
                    if (
                        !Quaternion.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ROTATION)),
                                    message)),
                            out rotation))
                    {
                        rotation = Quaternion.CreateFromEulers(0, 0, 0);
                    }
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
                    if (!GetParcelAtPosition(simulator, position, ref parcel))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_FIND_PARCEL);
                    }
                    if (!parcel.OwnerID.Equals(Client.Self.AgentID))
                    {
                        throw new ScriptException(ScriptError.PARCEL_MUST_BE_OWNED);
                    }
                    Vector3 scale;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.SCALE)),
                                    message)),
                            out scale))
                    {
                        scale = new Vector3(0.5f, 0.5f, 0.5f);
                    }
                    if (IsSecondLife() &&
                        ((scale.X < LINDEN_CONSTANTS.PRIMITIVES.MINIMUM_SIZE_X ||
                          scale.Y < LINDEN_CONSTANTS.PRIMITIVES.MINIMUM_SIZE_Y ||
                          scale.Z < LINDEN_CONSTANTS.PRIMITIVES.MINIMUM_SIZE_Z ||
                          scale.X > LINDEN_CONSTANTS.PRIMITIVES.MAXIMUM_SIZE_X ||
                          scale.Y > LINDEN_CONSTANTS.PRIMITIVES.MAXIMUM_SIZE_Y ||
                          scale.Z > LINDEN_CONSTANTS.PRIMITIVES.MAXIMUM_SIZE_Z)))
                    {
                        throw new ScriptException(ScriptError.SCALE_WOULD_EXCEED_BUILDING_CONSTRAINTS);
                    }
                    bool newTree;
                    if (
                        !bool.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.NEW)),
                                    message)),
                            out newTree))
                    {
                        newTree = true;
                    }
                    FieldInfo treeFieldInfo = typeof (Tree).GetFields(
                        BindingFlags.Public |
                        BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    wasInput(
                                        wasKeyValueGet(
                                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)),
                                            message)),
                                    StringComparison.OrdinalIgnoreCase));
                    if (treeFieldInfo == null)
                    {
                        throw new ScriptException(ScriptError.UNKNOWN_TREE_TYPE);
                    }
                    // Finally, add the tree to the simulator.
                    Client.Objects.AddTree(simulator, scale, rotation, position, (Tree) treeFieldInfo.GetValue(null),
                        commandGroup.UUID, newTree);
                };
        }
    }
}