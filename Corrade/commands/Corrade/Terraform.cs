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

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> terraform =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    int amount;
                    if (!int.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.AMOUNT)),
                            corradeCommandParameters.Message)), out amount))
                    {
                        throw new ScriptException(ScriptError.INVALID_AMOUNT);
                    }
                    float width;
                    if (!float.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.WIDTH)),
                            corradeCommandParameters.Message)), out width))
                    {
                        throw new ScriptException(ScriptError.INVALID_WIDTH);
                    }
                    float height;
                    if (!float.TryParse(wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.HEIGHT)),
                            corradeCommandParameters.Message)), out height))
                    {
                        throw new ScriptException(ScriptError.INVALID_HEIGHT);
                    }
                    string action = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(action))
                    {
                        throw new ScriptException(ScriptError.NO_TERRAFORM_ACTION_SPECIFIED);
                    }
                    FieldInfo terraformActionFieldInfo = typeof (TerraformAction).GetFields(
                        BindingFlags.Public |
                        BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(action,
                                    StringComparison.OrdinalIgnoreCase));
                    if (terraformActionFieldInfo == null)
                    {
                        throw new ScriptException(ScriptError.INVALID_TERRAFORM_ACTION);
                    }
                    TerraformAction terraformAction = (TerraformAction) terraformActionFieldInfo.GetValue(null);
                    string brush = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.BRUSH)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(brush))
                    {
                        throw new ScriptException(ScriptError.NO_TERRAFORM_BRUSH_SPECIFIED);
                    }
                    FieldInfo terraformBrushFieldInfo = typeof (TerraformBrushSize).GetFields(
                        BindingFlags.Public |
                        BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(brush,
                                    StringComparison.OrdinalIgnoreCase));
                    if (terraformBrushFieldInfo == null)
                    {
                        throw new ScriptException(ScriptError.INVALID_TERRAFORM_BRUSH);
                    }
                    TerraformBrushSize terraformBrush = (TerraformBrushSize) terraformBrushFieldInfo.GetValue(null);
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
                    string region =
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
                    if (!simulator.IsEstateManager)
                    {
                        if (!parcel.OwnerID.Equals(Client.Self.AgentID) &&
                            !parcel.Flags.HasFlag(ParcelFlags.AllowTerraform))
                        {
                            if (!parcel.IsGroupOwned && !parcel.GroupID.Equals(corradeCommandParameters.Group.UUID))
                            {
                                throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                            }
                            if (
                                !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                    corradeCommandParameters.Group.UUID,
                                    GroupPowers.AllowEditLand,
                                    corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                    new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                            {
                                throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
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
                            throw new ScriptException(ScriptError.COULD_NOT_TERRAFORM);
                        }
                    }
                };
        }
    }
}