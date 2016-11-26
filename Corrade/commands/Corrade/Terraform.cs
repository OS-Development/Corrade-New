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
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    int amount;
                    if (!int.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.AMOUNT)),
                            corradeCommandParameters.Message)), out amount))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_AMOUNT);
                    }
                    float width;
                    if (!float.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.WIDTH)),
                            corradeCommandParameters.Message)), out width))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_WIDTH);
                    }
                    float height;
                    if (!float.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.HEIGHT)),
                            corradeCommandParameters.Message)), out height))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_HEIGHT);
                    }
                    var action = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(action))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_TERRAFORM_ACTION_SPECIFIED);
                    }
                    var terraformActionFieldInfo = typeof(TerraformAction).GetFields(
                        BindingFlags.Public |
                        BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(action,
                                    StringComparison.OrdinalIgnoreCase));
                    if (terraformActionFieldInfo == null)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_TERRAFORM_ACTION);
                    }
                    var terraformAction = (TerraformAction) terraformActionFieldInfo.GetValue(null);
                    var brush = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.BRUSH)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(brush))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_TERRAFORM_BRUSH_SPECIFIED);
                    }
                    var terraformBrushFieldInfo = typeof(TerraformBrushSize).GetFields(
                        BindingFlags.Public |
                        BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(brush,
                                    StringComparison.OrdinalIgnoreCase));
                    if (terraformBrushFieldInfo == null)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.INVALID_TERRAFORM_BRUSH);
                    }
                    var terraformBrush = (TerraformBrushSize) terraformBrushFieldInfo.GetValue(null);
                    Vector3 position;
                    if (
                        !Vector3.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                    corradeCommandParameters.Message)),
                            out position))
                    {
                        position = Client.Self.SimPosition;
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
                    if (!simulator.IsEstateManager)
                    {
                        if (!parcel.OwnerID.Equals(Client.Self.AgentID) &&
                            !parcel.Flags.HasFlag(ParcelFlags.AllowTerraform))
                        {
                            if (!parcel.IsGroupOwned && !parcel.GroupID.Equals(corradeCommandParameters.Group.UUID))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                            }
                            if (
                                !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                    corradeCommandParameters.Group.UUID,
                                    GroupPowers.AllowEditLand,
                                    corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                    new DecayingAlarm(corradeConfiguration.DataDecayType)))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                            }
                        }
                    }
                    lock (Locks.ClientInstanceParcelsLock)
                    {
                        if (
                            !Client.Parcels.Terraform(simulator, -1, position.X - width, position.Y - height,
                                position.X + width,
                                position.Y + height, terraformAction, terraformBrush, amount))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_TERRAFORM);
                        }
                    }
                };
        }
    }
}