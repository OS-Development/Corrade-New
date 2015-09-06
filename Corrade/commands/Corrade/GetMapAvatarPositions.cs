///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> getmapavatarpositions =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string region =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REGION)),
                            message));
                    if (string.IsNullOrEmpty(region))
                    {
                        region = Client.Network.CurrentSim.Name;
                    }
                    ulong regionHandle = 0;
                    ManualResetEvent GridRegionEvent = new ManualResetEvent(false);
                    EventHandler<GridRegionEventArgs> GridRegionEventHandler = (sender, args) =>
                    {
                        if (!args.Region.Name.Equals(region, StringComparison.InvariantCultureIgnoreCase))
                            return;
                        regionHandle = args.Region.RegionHandle;
                        GridRegionEvent.Set();
                    };
                    lock (ClientInstanceGridLock)
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
                        new List<string>(mapItems.AsParallel()
                            .Where(o => (o as MapAgentLocation) != null)
                            .Select(o => new[]
                            {
                                ((MapAgentLocation) o).AvatarCount.ToString(CultureInfo.DefaultThreadCurrentCulture),
                                new Vector3(o.LocalX, o.LocalY, 0).ToString()
                            }).SelectMany(o => o));
                    if (data.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(data));
                    }
                };
        }
    }
}