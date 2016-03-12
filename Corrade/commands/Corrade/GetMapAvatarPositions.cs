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

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getmapavatarpositions =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string region =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.REGION)),
                                corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(region))
                    {
                        region = Client.Network.CurrentSim.Name;
                    }
                    ulong regionHandle = 0;
                    ManualResetEvent GridRegionEvent = new ManualResetEvent(false);
                    EventHandler<GridRegionEventArgs> GridRegionEventHandler = (sender, args) =>
                    {
                        if (!string.Equals(region, args.Region.Name, StringComparison.OrdinalIgnoreCase))
                            return;
                        regionHandle = args.Region.RegionHandle;
                        GridRegionEvent.Set();
                    };
                    lock (Locks.ClientInstanceGridLock)
                    {
                        Client.Grid.GridRegion += GridRegionEventHandler;
                        Client.Grid.RequestMapRegion(region, GridLayerType.Objects);
                        if (!GridRegionEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Grid.GridRegion -= GridRegionEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_REGION);
                        }
                        Client.Grid.GridRegion -= GridRegionEventHandler;
                    }
                    if (regionHandle.Equals(0))
                    {
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    }
                    HashSet<MapItem> mapItems =
                        new HashSet<MapItem>(Client.Grid.MapItems(regionHandle, GridItemType.AgentLocations,
                            GridLayerType.Objects, (int) corradeConfiguration.ServicesTimeout));
                    if (!mapItems.Any())
                    {
                        throw new ScriptException(ScriptError.NO_MAP_ITEMS_FOUND);
                    }
                    List<string> data =
                        mapItems.AsParallel()
                            .Where(o => (o as MapAgentLocation) != null)
                            .Select(o => new[]
                            {
                                ((MapAgentLocation) o).AvatarCount.ToString(Utils.EnUsCulture),
                                new Vector3(o.LocalX, o.LocalY, 0).ToString()
                            }).SelectMany(o => o).ToList();
                    if (data.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(data));
                    }
                };
        }
    }
}