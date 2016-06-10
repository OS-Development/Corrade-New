///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> searchinventory =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var assetTypes = new HashSet<AssetType>();
                    var LockObject = new object();
                    CSV.ToEnumerable(
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                            corradeCommandParameters.Message)))
                        .ToArray()
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o))
                        .ForAll(
                            o => typeof (AssetType).GetFields(BindingFlags.Public | BindingFlags.Static)
                                .AsParallel().Where(p => string.Equals(o, p.Name, StringComparison.Ordinal)).ForAll(
                                    q =>
                                    {
                                        lock (LockObject)
                                        {
                                            assetTypes.Add((AssetType) q.GetValue(null));
                                        }
                                    }));
                    var pattern =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.PATTERN)),
                                corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(pattern))
                    {
                        throw new ScriptException(ScriptError.NO_PATTERN_PROVIDED);
                    }
                    Regex search;
                    try
                    {
                        search = new Regex(pattern, RegexOptions.Compiled);
                    }
                    catch
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_COMPILE_REGULAR_EXPRESSION);
                    }
                    var csv = new List<string>();
                    Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode, search)
                        .ToArray()
                        .AsParallel()
                        .ForAll(
                            o =>
                            {
                                var inventoryItem = o as InventoryItem;
                                if (inventoryItem == null) return;
                                if (assetTypes.Any() && !assetTypes.Contains(inventoryItem.AssetType))
                                    return;
                                lock (LockObject)
                                {
                                    csv.AddRange(new[]
                                    {
                                        Enum.GetName(typeof (AssetType), inventoryItem.AssetType),
                                        inventoryItem.Name,
                                        inventoryItem.AssetUUID.ToString()
                                    });
                                }
                            });
                    if (csv.Any())
                    {
                        result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                    }
                };
        }
    }
}