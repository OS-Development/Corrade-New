///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using CorradeConfigurationSharp;
using OpenMetaverse;
using OpenMetaverse.Messages.Linden;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> getparcelobjectsresourcedetaildata =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int)Configuration.Permissions.Land))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
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
                    UUID parcelUUID = Client.Parcels.RequestRemoteParcelID(position, simulator.Handle, UUID.Zero);
                    // Establish CAPs connection if not established.
                    if (!Client.Network.CurrentSim.Caps.IsEventQueueRunning)
                    {
                        var EventQueueRunningEvent = new AutoResetEvent(false);
                        EventHandler<EventQueueRunningEventArgs> handler = (sender, e) => { EventQueueRunningEvent.Set(); };
                        Client.Network.EventQueueRunning += handler;
                        EventQueueRunningEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, false);
                        Client.Network.EventQueueRunning -= handler;
                    }

                    var succeeded = false;
                    LandResourcesInfo landResourcesInfo = null;
                    ManualResetEvent ParcelResourcesReceivedEvent = new ManualResetEvent(false);
                    lock (Locks.ClientInstanceParcelsLock)
                    {
                        Client.Parcels.GetParcelResouces(parcelUUID, true, (success, info) =>
                        {
                            succeeded = success;
                            landResourcesInfo = info;
                            ParcelResourcesReceivedEvent.Set();
                        });

                        ParcelResourcesReceivedEvent.WaitOne((int)corradeConfiguration.ServicesTimeout);
                    }

                    if (!succeeded)
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_GET_LAND_RESOURCES);

                    var parcelResourceDetail = landResourcesInfo.Parcels.AsParallel().FirstOrDefault(o => o.LocalID.Equals(parcel.LocalID));
                    if (parcelResourceDetail == null)
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_GET_PARCEL_RESOURCES);

                    var data = new List<string>();
                    object LockObject = new object();
                    parcelResourceDetail.Objects.AsParallel().ForAll(o =>
                    {
                        lock (LockObject)
                        {
                            data.AddRange(o.GetStructuredData(
                            wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                corradeCommandParameters.Message))));
                        }
                    });
                    if (data.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                    }
                };
        }
    }
}
