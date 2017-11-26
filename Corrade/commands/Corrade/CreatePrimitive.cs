///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading;
using Corrade.Constants;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Timers;
using Inventory = wasOpenMetaverse.Inventory;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> createprimitive
                =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                    var name = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                        corradeCommandParameters.Message));
                    var description = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                        corradeCommandParameters.Message));
                    var permissions = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PERMISSIONS)),
                        corradeCommandParameters.Message));

                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_POSITION);
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                        position.Z > wasOpenMetaverse.Constants.PRIMITIVES.MAXIMUM_REZ_HEIGHT)
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.POSITION_WOULD_EXCEED_MAXIMUM_REZ_ALTITUDE);
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
                    if (!simulator.IsEstateManager && !parcel.Flags.IsMaskFlagSet(ParcelFlags.CreateObjects) &&
                        !parcel.OwnerID.Equals(Client.Self.AgentID) &&
                        (!parcel.Flags.IsMaskFlagSet(ParcelFlags.CreateGroupObjects) ||
                         !Services.HasGroupPowers(Client, Client.Self.AgentID,
                             parcel.GroupID,
                             GroupPowers.AllowRez,
                             corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                             new DecayingAlarm(corradeConfiguration.DataDecayType))))
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    Vector3 scale;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SCALE)),
                                    corradeCommandParameters.Message)),
                            out scale))
                        scale = wasOpenMetaverse.Constants.PRIMITIVES.DEFAULT_NEW_PRIMITIVE_SCALE;
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                        (scale.X < wasOpenMetaverse.Constants.PRIMITIVES.MINIMUM_SIZE_X ||
                         scale.Y < wasOpenMetaverse.Constants.PRIMITIVES.MINIMUM_SIZE_Y ||
                         scale.Z < wasOpenMetaverse.Constants.PRIMITIVES.MINIMUM_SIZE_Z ||
                         scale.X > wasOpenMetaverse.Constants.PRIMITIVES.MAXIMUM_SIZE_X ||
                         scale.Y > wasOpenMetaverse.Constants.PRIMITIVES.MAXIMUM_SIZE_Y ||
                         scale.Z > wasOpenMetaverse.Constants.PRIMITIVES.MAXIMUM_SIZE_Z))
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.SCALE_WOULD_EXCEED_BUILDING_CONSTRAINTS);
                    // build the primitive shape from presets by supplying "type" (or not)...
                    var primitiveShapesFieldInfo = typeof(CORRADE_CONSTANTS.PRIMTIVE_BODIES).GetFields(
                            BindingFlags.Public |
                            BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
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
                    constructionData =
                        constructionData.wasCSVToStructure(
                            wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                corradeCommandParameters.Message)), wasInput);
                    // Get any primitive flags.
                    var primFlags = PrimFlags.None;
                    CSV.ToEnumerable(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FLAGS)),
                                    corradeCommandParameters.Message)))
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o))
                        .ForAll(
                            o =>
                                typeof(PrimFlags).GetFields(BindingFlags.Public | BindingFlags.Static)
                                    .AsParallel()
                                    .Where(p => string.Equals(o, p.Name, StringComparison.Ordinal))
                                    .ForAll(
                                        q =>
                                        {
                                            BitTwiddling.SetMaskFlag(ref primFlags, (PrimFlags) q.GetValue(null));
                                        }));

                    // Listen for newly created primitives.
                    var PrimitiveCreatedEvent = new ManualResetEventSlim(false);
                    EventHandler<PrimEventArgs> ObjectUpdateEventHandler = (sender, args) =>
                    {
                        // Skip updates for objects we did not create.
                        if (!args.Prim.Flags.IsMaskFlagSet(PrimFlags.CreateSelected))
                            return;

                        // Return the new object UUID.
                        result.Add(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                            args.Prim.ID.ToString());

                        // Move primitive to final destination to be precise.
                        Client.Objects.SetPosition(simulator,
                            args.Prim.LocalID, position);

                        // Set the primitive name.
                        if (!string.IsNullOrEmpty(name))
                            Client.Objects.SetName(simulator,
                                args.Prim.LocalID, name);

                        // Set the primitive description.
                        if (!string.IsNullOrEmpty(description))
                            Client.Objects.SetDescription(simulator,
                                args.Prim.LocalID, description);

                        // Set permissions if requested.
                        if (!string.IsNullOrEmpty(permissions))
                        {
                            var objectPermissions = Permissions.NoPermissions;
                            Inventory.wasStringToPermissions(permissions, out objectPermissions);
                            var primitiveList = new List<uint> {args.Prim.LocalID};

                            // Set primitive permissions.
                            Client.Objects.SetPermissions(simulator, primitiveList, PermissionWho.Group,
                                objectPermissions.GroupMask, true);
                            Client.Objects.SetPermissions(simulator, primitiveList,
                                PermissionWho.Everyone,
                                objectPermissions.EveryoneMask, true);
                            Client.Objects.SetPermissions(simulator, primitiveList,
                                PermissionWho.NextOwner,
                                objectPermissions.NextOwnerMask, true);
                        }

                        PrimitiveCreatedEvent.Set();
                    };

                    // Activate parcel group.
                    Locks.ClientInstanceGroupsLock.EnterWriteLock();
                    Client.Groups.ActivateGroup(parcel.GroupID);

                    // Finally, add the primitive to the simulator.
                    Locks.ClientInstanceObjectsLock.EnterWriteLock();
                    Client.Objects.ObjectUpdate += ObjectUpdateEventHandler;
                    Client.Objects.AddPrim(simulator,
                        constructionData,
                        corradeCommandParameters.Group.UUID,
                        position,
                        scale,
                        rotation,
                        // Create the primitive selected in order to be able to change parameters.
                        primFlags | PrimFlags.CreateSelected);
                    if (!PrimitiveCreatedEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Objects.ObjectUpdate -= ObjectUpdateEventHandler;
                        Locks.ClientInstanceObjectsLock.ExitWriteLock();
                        // Activate the initial group.
                        Client.Groups.ActivateGroup(initialGroup);
                        Locks.ClientInstanceGroupsLock.ExitWriteLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_REZZING_PRIMITIVE);
                    }
                    Client.Objects.ObjectUpdate -= ObjectUpdateEventHandler;
                    Locks.ClientInstanceObjectsLock.ExitWriteLock();

                    // Activate the initial group.
                    Client.Groups.ActivateGroup(initialGroup);
                    Locks.ClientInstanceGroupsLock.ExitWriteLock();
                };
        }
    }
}