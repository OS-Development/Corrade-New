///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
                getgridregiondata =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Land))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        var region =
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                    corradeCommandParameters.Message));
                        UUID regionUUID;
                        switch (UUID.TryParse(region, out regionUUID))
                        {
                            case true:
                                ulong regionHandle = 0;
                                if (
                                    !Resolvers.RegionUUIDToHandle(Client, regionUUID,
                                        corradeConfiguration.ServicesTimeout,
                                        ref regionHandle))
                                    throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                                var parcelUUID = Client.Parcels.RequestRemoteParcelID(new Vector3(128, 128, 0),
                                    regionHandle,
                                    UUID.Zero);
                                if (parcelUUID.Equals(UUID.Zero))
                                    throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                                var parcelInfo = new ParcelInfo();
                                if (
                                    !Services.GetParcelInfo(Client, parcelUUID, corradeConfiguration.ServicesTimeout,
                                        ref parcelInfo))
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.COULD_NOT_GET_PARCEL_INFO);
                                region = parcelInfo.SimName;
                                break;

                            default:
                                if (string.IsNullOrEmpty(region))
                                {
                                    Locks.ClientInstanceNetworkLock.EnterReadLock();
                                    region = Client.Network.CurrentSim.Name;
                                    Locks.ClientInstanceNetworkLock.ExitReadLock();
                                }
                                break;
                        }
                        var GridRegionEvent = new ManualResetEventSlim(false);
                        var gridRegion = new GridRegion();
                        EventHandler<GridRegionEventArgs> GridRegionEventHandler = (sender, args) =>
                        {
                            if (!string.Equals(region, args.Region.Name, StringComparison.OrdinalIgnoreCase))
                                return;
                            gridRegion = args.Region;
                            GridRegionEvent.Set();
                        };
                        Locks.ClientInstanceGridLock.EnterReadLock();
                        Client.Grid.GridRegion += GridRegionEventHandler;
                        Client.Grid.RequestMapRegion(region, GridLayerType.Objects);
                        if (!GridRegionEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                        {
                            Client.Grid.GridRegion -= GridRegionEventHandler;
                            Locks.ClientInstanceGridLock.ExitReadLock();
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_REGION);
                        }
                        Client.Grid.GridRegion -= GridRegionEventHandler;
                        Locks.ClientInstanceGridLock.ExitReadLock();
                        switch (!gridRegion.Equals(default(GridRegion)))
                        {
                            case false:
                                throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                        }
                        var data =
                            gridRegion.GetStructuredData(
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