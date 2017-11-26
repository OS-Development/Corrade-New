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
                getparcelinfodata =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Land))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
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
                            !Services.GetParcelAtPosition(Client, simulator, position,
                                corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                ref parcel))
                            throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                        UUID parcelUUID;
                        Locks.ClientInstanceParcelsLock.EnterReadLock();
                        parcelUUID = Client.Parcels.RequestRemoteParcelID(position, simulator.Handle,
                            UUID.Zero);
                        Locks.ClientInstanceParcelsLock.ExitReadLock();
                        if (parcelUUID.Equals(UUID.Zero))
                            throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_FIND_PARCEL);
                        var ParcelInfoEvent = new ManualResetEventSlim(false);
                        var parcelInfo = new ParcelInfo();
                        EventHandler<ParcelInfoReplyEventArgs> ParcelInfoEventHandler = (sender, args) =>
                        {
                            if (!args.Parcel.ID.Equals(parcelUUID))
                                return;

                            parcelInfo = args.Parcel;
                            ParcelInfoEvent.Set();
                        };
                        Locks.ClientInstanceParcelsLock.EnterReadLock();
                        Client.Parcels.ParcelInfoReply += ParcelInfoEventHandler;
                        Client.Parcels.RequestParcelInfo(parcelUUID);
                        if (!ParcelInfoEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                        {
                            Client.Parcels.ParcelInfoReply -= ParcelInfoEventHandler;
                            Locks.ClientInstanceParcelsLock.ExitReadLock();
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_PARCELS);
                        }
                        Client.Parcels.ParcelInfoReply -= ParcelInfoEventHandler;
                        Locks.ClientInstanceParcelsLock.ExitReadLock();
                        if (parcelInfo.Equals(default(ParcelInfo)))
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