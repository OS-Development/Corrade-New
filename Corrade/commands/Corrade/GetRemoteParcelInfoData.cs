///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2016 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfigurationSharp;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getremoteparcelinfodata =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Land))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        Vector3 global;
                        if (
                            !Vector3.TryParse(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                        corradeCommandParameters.Message)),
                                out global))
                            global = new Vector3((float) Client.Self.GlobalPosition.X,
                                (float) Client.Self.GlobalPosition.Y, (float) Client.Self.GlobalPosition.Z);
                        var local = Vector3.Zero;
                        var simHandle = OpenMetaverse.Helpers.GlobalPosToRegionHandle(global.X, global.Y, out local.X,
                            out local.Y);
                        if (simHandle.Equals(0))
                            throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                        var parcelUUID = Client.Parcels.RequestRemoteParcelID(local, simHandle, UUID.Zero);
                        if (parcelUUID.Equals(UUID.Zero))
                            throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                        var parcelInfo = new ParcelInfo();
                        if (
                            !Services.GetParcelInfo(Client, parcelUUID, corradeConfiguration.ServicesTimeout,
                                ref parcelInfo))
                            throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_GET_PARCEL_INFO);
                        var data =
                            parcelInfo.GetStructuredData(
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message))).ToList();
                        if (data.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(data));
                    };
        }
    }
}