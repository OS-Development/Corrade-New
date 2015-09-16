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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> getgridregiondata =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string region =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REGION)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(region))
                    {
                        region = Client.Network.CurrentSim.Name;
                    }
                    ManualResetEvent GridRegionEvent = new ManualResetEvent(false);
                    GridRegion gridRegion = new GridRegion();
                    EventHandler<GridRegionEventArgs> GridRegionEventHandler = (sender, args) =>
                    {
                        if (!args.Region.Name.Equals(region, StringComparison.OrdinalIgnoreCase))
                            return;
                        gridRegion = args.Region;
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
                    switch (!gridRegion.Equals(default(GridRegion)))
                    {
                        case false:
                            throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    }
                    List<string> data = new List<string>(GetStructuredData(gridRegion,
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DATA)),
                            corradeCommandParameters.Message))));
                    if (data.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(data));
                    }
                };
        }
    }
}