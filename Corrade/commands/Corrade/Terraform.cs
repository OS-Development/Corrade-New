///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> terraform =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Land))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    int amount;
                    if (!int.TryParse(wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AMOUNT)),
                                corradeCommandParameters.Message)), NumberStyles.Integer,
                        Utils.EnUsCulture, out amount))
                        amount = 1;
                    float altitude;
                    if (!float.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ALTITUDE)),
                            corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture, out altitude))
                        altitude = 0;
                    float width;
                    if (!float.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.WIDTH)),
                            corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture, out width))
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_WIDTH);
                    float height;
                    if (!float.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.HEIGHT)),
                            corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture, out height))
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_HEIGHT);
                    var action = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(action))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_TERRAFORM_ACTION_SPECIFIED);
                    var terraformActionFieldInfo = typeof(TerraformAction).GetFields(
                            BindingFlags.Public |
                            BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(action,
                                    StringComparison.OrdinalIgnoreCase));
                    if (terraformActionFieldInfo == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_TERRAFORM_ACTION);
                    var terraformAction = (TerraformAction) terraformActionFieldInfo.GetValue(null);
                    var brush = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.BRUSH)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(brush))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_TERRAFORM_BRUSH_SPECIFIED);
                    var terraformBrushFieldInfo = typeof(TerraformBrushSize).GetFields(
                            BindingFlags.Public |
                            BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(brush,
                                    StringComparison.OrdinalIgnoreCase));
                    if (terraformBrushFieldInfo == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_TERRAFORM_BRUSH);
                    var terraformBrush = (TerraformBrushSize) terraformBrushFieldInfo.GetValue(null);
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                        position = Client.Self.SimPosition;
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
                    if (!simulator.IsEstateManager &&
                        !parcel.Flags.IsMaskFlagSet(ParcelFlags.AllowTerraform) &&
                        !parcel.OwnerID.Equals(Client.Self.AgentID) &&
                        !Services.HasGroupPowers(Client, Client.Self.AgentID,
                            parcel.GroupID,
                            GroupPowers.AllowEditLand,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                            new DecayingAlarm(corradeConfiguration.DataDecayType)))
                        throw new Command.ScriptException(
                            Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);

                    // Activate parcel group.
                    Locks.ClientInstanceGroupsLock.EnterWriteLock();
                    Client.Groups.ActivateGroup(parcel.GroupID);

                    Locks.ClientInstanceParcelsLock.EnterWriteLock();
                    Client.Parcels.Terraform(simulator, -1, position.X - width, position.Y - height,
                        position.X + width,
                        position.Y + height, terraformAction, terraformBrush, amount, altitude);
                    Locks.ClientInstanceParcelsLock.ExitWriteLock();

                    // Activate the initial group.
                    Client.Groups.ActivateGroup(initialGroup);
                    Locks.ClientInstanceGroupsLock.ExitWriteLock();
                };
        }
    }
}