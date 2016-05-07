///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
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
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
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
                        lock (Locks.ClientInstanceNetworkLock)
                        {
                            region = Client.Network.CurrentSim.Name;
                        }
                    }
                    ulong regionHandle = 0;
                    if (
                        !Resolvers.RegionNameToHandle(Client, region, corradeConfiguration.ServicesTimeout,
                            ref regionHandle))
                    {
                        throw new ScriptException(ScriptError.REGION_NOT_FOUND);
                    }
                    HashSet<MapItem> mapItems = new HashSet<MapItem>();
                    lock (Locks.ClientInstanceGridLock)
                    {
                        mapItems.UnionWith(Client.Grid.MapItems(regionHandle, GridItemType.AgentLocations,
                            GridLayerType.Objects, (int) corradeConfiguration.ServicesTimeout));
                    }
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