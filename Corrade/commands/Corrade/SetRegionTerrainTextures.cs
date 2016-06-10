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
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> setregionterraintextures =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int) Configuration.Permissions.Land))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    if (!Client.Network.CurrentSim.IsEstateManager)
                    {
                        throw new ScriptException(ScriptError.NO_LAND_RIGHTS);
                    }
                    List<UUID> simTextures;
                    lock (Locks.ClientInstanceNetworkLock)
                    {
                        simTextures = new List<UUID>
                        {
                            Client.Network.CurrentSim.TerrainDetail0,
                            Client.Network.CurrentSim.TerrainDetail1,
                            Client.Network.CurrentSim.TerrainDetail2,
                            Client.Network.CurrentSim.TerrainDetail3
                        };
                    }
                    var setTextures = new UUID[4];
                    var data = CSV.ToEnumerable(
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DATA)),
                            corradeCommandParameters.Message))).ToList();
                    Enumerable.Range(0, 4).ToArray().AsParallel().ForAll(
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
                                                    Client.Inventory.Store.RootNode, data[o]
                                                    ).FirstOrDefault();
                                            switch (inventoryBaseItem is InventoryTexture)
                                            {
                                                case true:
                                                    setTextures[o] = (inventoryBaseItem as InventoryTexture).AssetUUID;
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
                    lock (Locks.ClientInstanceEstateLock)
                    {
                        Client.Estate.SetRegionTerrain(setTextures[0], setTextures[1], setTextures[2], setTextures[3]);
                    }
                };
        }
    }
}