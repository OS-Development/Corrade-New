///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Constants;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                setregionterraintextures
                    =
                    (corradeCommandParameters, result) =>
                    {
                        if (
                            !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Land))
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                        if (!Client.Network.CurrentSim.IsEstateManager)
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_LAND_RIGHTS);
                        Locks.ClientInstanceNetworkLock.EnterReadLock();
                        var simTextures = new List<UUID>
                        {
                            Client.Network.CurrentSim.TerrainDetail0,
                            Client.Network.CurrentSim.TerrainDetail1,
                            Client.Network.CurrentSim.TerrainDetail2,
                            Client.Network.CurrentSim.TerrainDetail3
                        };
                        Locks.ClientInstanceNetworkLock.ExitReadLock();
                        var setTextures = new UUID[4];
                        var data = CSV.ToEnumerable(
                            wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                corradeCommandParameters.Message))).ToList();
                        Enumerable.Range(0, 4).AsParallel().ForAll(
                            o =>
                            {
                                switch (data.ElementAtOrDefault(o) != null)
                                {
                                    case true:
                                        UUID textureUUID;
                                        switch (UUID.TryParse(data[o], out textureUUID))
                                        {
                                            case true:
                                                setTextures[o] = textureUUID;
                                                break;

                                            default:
                                                var inventoryBaseItem =
                                                    Inventory.FindInventory<InventoryBase>(Client,
                                                        data[o],
                                                        CORRADE_CONSTANTS.PATH_SEPARATOR,
                                                        CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                                        corradeConfiguration.ServicesTimeout);
                                                switch (inventoryBaseItem is InventoryTexture)
                                                {
                                                    case true:
                                                        setTextures[o] =
                                                            (inventoryBaseItem as InventoryTexture).AssetUUID;
                                                        break;

                                                    default:
                                                        setTextures[o] = simTextures[o];
                                                        break;
                                                }
                                                break;
                                        }
                                        break;

                                    default:
                                        setTextures[o] = simTextures[o];
                                        break;
                                }
                            });
                        Locks.ClientInstanceEstateLock.EnterWriteLock();
                        Client.Estate.SetRegionTerrain(setTextures[0], setTextures[1], setTextures[2],
                            setTextures[3]);
                        Locks.ClientInstanceEstateLock.ExitWriteLock();
                    };
        }
    }
}