///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                getregionparcelsboundingbox =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Land))
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
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
                        // Get all sim parcels
                        var SimParcelsDownloadedEvent = new ManualResetEvent(false);
                        EventHandler<SimParcelsDownloadedEventArgs> SimParcelsDownloadedEventHandler =
                            (sender, args) => SimParcelsDownloadedEvent.Set();
                        lock (Locks.ClientInstanceParcelsLock)
                        {
                            Client.Parcels.SimParcelsDownloaded += SimParcelsDownloadedEventHandler;
                            Client.Parcels.RequestAllSimParcels(simulator);
                            if (simulator.IsParcelMapFull())
                            {
                                SimParcelsDownloadedEvent.Set();
                            }
                            if (!SimParcelsDownloadedEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                            {
                                Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                                throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_PARCELS);
                            }
                            Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                        }
                        var csv = new List<Vector3>();
                        simulator.Parcels.ForEach(o => csv.AddRange(new[] {o.AABBMin, o.AABBMax}));
                        if (csv.Any())
                        {
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(csv.Select(o => o.ToString())));
                        }
                    };
        }
    }
}