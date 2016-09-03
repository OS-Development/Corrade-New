﻿///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
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
            public static Action<Command.CorradeCommandParameters, Dictionary<string, string>> setregioninfo =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Land))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    if (!Client.Network.CurrentSim.IsEstateManager)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_LAND_RIGHTS);
                    }
                    bool terraform;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TERRAFORM)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant(), out terraform))
                    {
                        terraform = !Client.Network.CurrentSim.Flags.HasFlag(RegionFlags.BlockTerraform);
                    }
                    bool fly;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FLY)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant(), out fly))
                    {
                        fly = !Client.Network.CurrentSim.Flags.HasFlag(RegionFlags.NoFly);
                    }
                    bool damage;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DAMAGE)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant(), out damage))
                    {
                        damage = Client.Network.CurrentSim.Flags.HasFlag(RegionFlags.AllowDamage);
                    }
                    bool resell;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RESELL)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant(), out resell))
                    {
                        resell = !Client.Network.CurrentSim.Flags.HasFlag(RegionFlags.BlockLandResell);
                    }
                    bool push;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PUSH)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant(), out push))
                    {
                        push = !Client.Network.CurrentSim.Flags.HasFlag(RegionFlags.RestrictPushObject);
                    }
                    bool parcel;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PARCEL)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant(), out parcel))
                    {
                        parcel = Client.Network.CurrentSim.Flags.HasFlag(RegionFlags.AllowParcelChanges);
                    }
                    float limit;
                    if (
                        !float.TryParse(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.LIMIT)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant(), out limit))
                    {
                        limit = wasOpenMetaverse.Constants.REGION.DEFAULT_AGENT_LIMIT;
                    }
                    float bonus;
                    if (
                        !float.TryParse(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.BONUS)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant(), out bonus))
                    {
                        bonus = wasOpenMetaverse.Constants.REGION.DEFAULT_OBJECT_BONUS;
                    }
                    bool mature;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MATURE)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant(), out mature))
                    {
                        mature = Client.Network.CurrentSim.Access.Equals(SimAccess.Mature);
                    }
                    lock (Locks.ClientInstanceEstateLock)
                    {
                        Client.Estate.SetRegionInfo(!terraform, !fly, damage, resell, !push, parcel, limit, bonus,
                            mature);
                    }
                };
        }
    }
}