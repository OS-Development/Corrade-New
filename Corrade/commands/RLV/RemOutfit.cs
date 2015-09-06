using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> remoutfit = (message, rule, senderUUID) =>
            {
                if (!rule.Param.Equals(RLV_CONSTANTS.FORCE))
                {
                    return;
                }
                InventoryBase inventoryBase;
                switch (!string.IsNullOrEmpty(rule.Option))
                {
                    case true: // A single wearable
                        FieldInfo wearTypeInfo = typeof (WearableType).GetFields(BindingFlags.Public |
                                                                                 BindingFlags.Static)
                            .AsParallel().FirstOrDefault(
                                p => p.Name.Equals(rule.Option, StringComparison.InvariantCultureIgnoreCase));
                        if (wearTypeInfo == null)
                        {
                            break;
                        }
                        KeyValuePair<AppearanceManager.WearableData, WearableType> wearable = GetWearables(
                            Client.Inventory.Store.RootNode)
                            .AsParallel().FirstOrDefault(
                                o => o.Value.Equals((WearableType) wearTypeInfo.GetValue(null)));
                        switch (
                            !wearable.Equals(default(KeyValuePair<AppearanceManager.WearableData, WearableType>))
                            )
                        {
                            case true:
                                inventoryBase = FindInventory<InventoryBase>(Client.Inventory.Store.RootNode,
                                    wearable.Value).FirstOrDefault();
                                if (inventoryBase != null)
                                    UnWear(inventoryBase as InventoryItem);
                                break;
                        }
                        break;
                    default:
                        Parallel.ForEach(GetWearables(Client.Inventory.Store.RootNode)
                            .AsParallel().Select(o => new[]
                            {
                                o.Key
                            }).SelectMany(o => o), o =>
                            {
                                inventoryBase =
                                    FindInventory<InventoryBase>(Client.Inventory.Store.RootNode, o.ItemID
                                        )
                                        .FirstOrDefault(p => (p is InventoryWearable));
                                if (inventoryBase == null)
                                {
                                    return;
                                }
                                UnWear(inventoryBase as InventoryItem);
                            });
                        break;
                }
                RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
            };
        }
    }
}