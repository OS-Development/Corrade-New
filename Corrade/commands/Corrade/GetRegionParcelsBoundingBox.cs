///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getregionparcelsboundingbox =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string region =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REGION)),
                            corradeCommandParameters.Message));
                    Simulator simulator =
                        Client.Network.Simulators.FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    string.IsNullOrEmpty(region) ? Client.Network.CurrentSim.Name : region,
                                    StringComparison.OrdinalIgnoreCase));
                    if (simulator == null)
                    {
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    }
                    // Get all sim parcels
                    ManualResetEvent SimParcelsDownloadedEvent = new ManualResetEvent(false);
                    EventHandler<SimParcelsDownloadedEventArgs> SimParcelsDownloadedEventHandler =
                        (sender, args) => SimParcelsDownloadedEvent.Set();
                    lock (ClientInstanceParcelsLock)
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
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_PARCELS);
                        }
                        Client.Parcels.SimParcelsDownloaded -= SimParcelsDownloadedEventHandler;
                    }
                    List<Vector3> csv = new List<Vector3>();
                    simulator.Parcels.ForEach(o => csv.AddRange(new[] {o.AABBMin, o.AABBMax}));
                    if (csv.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(csv.Select(o => o.ToString())));
                    }
                };
        }
    }
}