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
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> searchinventory
                =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var assetTypes = new HashSet<AssetType>();
                    var LockObject = new object();
                    CSV.ToEnumerable(
                            wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                corradeCommandParameters.Message)))
                        .AsParallel()
                        .Where(o => !string.IsNullOrEmpty(o))
                        .ForAll(
                            o => typeof(AssetType).GetFields(BindingFlags.Public | BindingFlags.Static)
                                .AsParallel()
                                .Where(p => string.Equals(o, p.Name, StringComparison.Ordinal))
                                .ForAll(
                                    q =>
                                    {
                                        lock (LockObject)
                                        {
                                            assetTypes.Add((AssetType) q.GetValue(null));
                                        }
                                    }));
                    var pattern =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PATTERN)),
                                corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(pattern))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_PATTERN_PROVIDED);
                    Regex search;
                    try
                    {
                        search = new Regex(pattern, RegexOptions.Compiled);
                    }
                    catch
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError
                            .COULD_NOT_COMPILE_REGULAR_EXPRESSION);
                    }
                    var csv = new List<string>();
                    Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode, search,
                            corradeConfiguration.ServicesTimeout)
                        .AsParallel()
                        .ForAll(
                            o =>
                            {
                                var assetType = string.Empty;
                                var name = string.Empty;
                                var itemUUID = UUID.Zero;
                                if (o is InventoryItem)
                                {
                                    var inventoryItem = o as InventoryItem;
                                    if (assetTypes.Any() && !assetTypes.Contains(inventoryItem.AssetType))
                                        return;
                                    assetType = Enum.GetName(typeof(AssetType), inventoryItem.AssetType);
                                    name = inventoryItem.Name;
                                    itemUUID = inventoryItem.UUID;
                                }
                                if (o is InventoryFolder)
                                {
                                    var inventoryFolder = o as InventoryFolder;
                                    if (assetTypes.Any() && !assetTypes.Contains(inventoryFolder.PreferredType))
                                        return;
                                    assetType = Enum.GetName(typeof(AssetType), inventoryFolder.PreferredType);
                                    name = inventoryFolder.Name;
                                    itemUUID = inventoryFolder.UUID;
                                }
                                lock (LockObject)
                                {
                                    csv.AddRange(new[]
                                    {
                                        assetType,
                                        name,
                                        itemUUID.ToString()
                                    });
                                }
                            });
                    if (csv.Any())
                        result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                            CSV.FromEnumerable(csv));
                };
        }
    }
}