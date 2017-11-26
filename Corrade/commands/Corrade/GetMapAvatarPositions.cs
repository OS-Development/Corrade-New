///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
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
                getmapavatarpositions =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Interact))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        var region =
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REGION)),
                                    corradeCommandParameters.Message));
                        if (string.IsNullOrEmpty(region))
                        {
                            Locks.ClientInstanceNetworkLock.EnterReadLock();
                            region = Client.Network.CurrentSim.Name;
                            Locks.ClientInstanceNetworkLock.ExitReadLock();
                        }
                        ulong regionHandle = 0;
                        if (
                            !Resolvers.RegionNameToHandle(Client, region, corradeConfiguration.ServicesTimeout,
                                ref regionHandle))
                            throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);
                        Locks.ClientInstanceGridLock.EnterReadLock();
                        var mapItems = new HashSet<MapItem>(Client.Grid.MapItems(regionHandle,
                            GridItemType.AgentLocations,
                            GridLayerType.Objects, (int) corradeConfiguration.ServicesTimeout));
                        Locks.ClientInstanceGridLock.ExitReadLock();
                        if (!mapItems.Any())
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_MAP_ITEMS_FOUND);
                        var data =
                            mapItems.AsParallel()
                                .Where(o => o as MapAgentLocation != null)
                                .Select(o => new[]
                                {
                                    ((MapAgentLocation) o).AvatarCount.ToString(Utils.EnUsCulture),
                                    new Vector3(o.LocalX, o.LocalY, 0).ToString()
                                }).SelectMany(o => o).ToList();
                        if (data.Any())
                            result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                CSV.FromEnumerable(data));
                    };
        }
    }
}