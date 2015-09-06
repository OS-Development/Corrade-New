using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> searchinventory =
                (commandGroup, message, result) =>
                {
                    if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Inventory))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    HashSet<AssetType> assetTypes = new HashSet<AssetType>();
                    object LockObject = new object();
                    Parallel.ForEach(wasCSVToEnumerable(
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)),
                            message))).AsParallel().Where(o => !string.IsNullOrEmpty(o)),
                        o => Parallel.ForEach(
                            typeof (AssetType).GetFields(BindingFlags.Public | BindingFlags.Static)
                                .AsParallel().Where(p => p.Name.Equals(o, StringComparison.Ordinal)),
                            q =>
                            {
                                lock (LockObject)
                                {
                                    assetTypes.Add((AssetType) q.GetValue(null));
                                }
                            }));
                    string pattern =
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.PATTERN)),
                            message));
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
                    List<string> csv = new List<string>();
                    Parallel.ForEach(FindInventory<InventoryBase>(Client.Inventory.Store.RootNode, search
                        ),
                        o =>
                        {
                            InventoryItem inventoryItem = o as InventoryItem;
                            if (inventoryItem == null) return;
                            if (assetTypes.Any() && !assetTypes.Contains(inventoryItem.AssetType))
                                return;
                            lock (LockObject)
                            {
                                csv.Add(Enum.GetName(typeof (AssetType), inventoryItem.AssetType));
                                csv.Add(inventoryItem.Name);
                                csv.Add(inventoryItem.AssetUUID.ToString());
                            }
                        });
                    if (csv.Any())
                    {
                        result.Add(wasGetDescriptionFromEnumValue(ResultKeys.DATA),
                            wasEnumerableToCSV(csv));
                    }
                };
        }
    }
}