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
using OpenMetaverse.Messages.Linden;
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
                getparcelobjectsresourcedetaildata =
                    (corradeCommandParameters, result) =>
                    {
                        if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
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
                        var data = wasInput(KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                            corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(data))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_DATA_PROVIDED);
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
                        var parcelUUID = Client.Parcels.RequestRemoteParcelID(position, simulator.Handle, UUID.Zero);
                        // Establish CAPs connection if not established.
                        if (!Client.Network.CurrentSim.Caps.IsEventQueueRunning)
                        {
                            var EventQueueRunningEvent = new AutoResetEvent(false);
                            EventHandler<EventQueueRunningEventArgs> handler = (sender, e) =>
                            {
                                EventQueueRunningEvent.Set();
                            };
                            Client.Network.EventQueueRunning += handler;
                            EventQueueRunningEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, true);
                            Client.Network.EventQueueRunning -= handler;
                        }

                        var succeeded = false;
                        LandResourcesInfo landResourcesInfo = null;
                        var ParcelResourcesReceivedEvent = new ManualResetEventSlim(false);
                        Locks.ClientInstanceParcelsLock.EnterReadLock();
                        Client.Parcels.GetParcelResouces(parcelUUID, true, (success, info) =>
                        {
                            succeeded = success;
                            landResourcesInfo = info;
                            ParcelResourcesReceivedEvent.Set();
                        });

                        ParcelResourcesReceivedEvent.Wait((int) corradeConfiguration.ServicesTimeout);
                        Locks.ClientInstanceParcelsLock.ExitReadLock();

                        if (!succeeded)
                            throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_GET_LAND_RESOURCES);

                        var parcelResourceDetail = landResourcesInfo.Parcels.AsParallel()
                            .FirstOrDefault(o => o.LocalID.Equals(parcel.LocalID));
                        if (parcelResourceDetail == null)
                            throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_GET_PARCEL_RESOURCES);

                        var csv = new List<string>();
                        var LockObject = new object();
                        parcelResourceDetail.Objects.AsParallel().ForAll(o =>
                        {
                            lock (LockObject)
                            {
                                csv.AddRange(o.GetStructuredData(data));
                            }
                        });
                        if (csv.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(csv));
                    };
        }
    }
}