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
using Helpers = wasOpenMetaverse.Helpers;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> setparceldata =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Land))
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
                    if (
                        !Services.GetParcelAtPosition(Client, simulator, position, corradeConfiguration.ServicesTimeout,
                            ref parcel))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_FIND_PARCEL);
                    }
                    if (!parcel.OwnerID.Equals(Client.Self.AgentID))
                    {
                        if (!parcel.IsGroupOwned && !parcel.GroupID.Equals(corradeCommandParameters.Group.UUID))
                        {
                            throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                        }
                    }
                    wasCSVToStructure(
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                            corradeCommandParameters.Message)), ref parcel);
                    if (Helpers.IsSecondLife(Client))
                    {
                        if (parcel.OtherCleanTime > Constants.PARCELS.MAXIMUM_AUTO_RETURN_TIME ||
                            parcel.OtherCleanTime < Constants.PARCELS.MINIMUM_AUTO_RETURN_TIME)
                        {
                            throw new ScriptException(ScriptError.AUTO_RETURN_TIME_OUTSIDE_LIMIT_RANGE);
                        }
                        if (parcel.Name.Length > Constants.PARCELS.MAXIMUM_NAME_LENGTH)
                        {
                            throw new ScriptException(ScriptError.NAME_TOO_LARGE);
                        }
                        if (parcel.Desc.Length > Constants.PARCELS.MAXIMUM_DESCRIPTION_LENGTH)
                        {
                            throw new ScriptException(ScriptError.DESCRIPTION_TOO_LARGE);
                        }
                    }
                    parcel.Update(simulator, true);
                };
        }
    }
}