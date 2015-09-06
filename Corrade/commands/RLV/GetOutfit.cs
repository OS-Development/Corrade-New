using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> getoutfit = (message, rule, senderUUID) =>
            {
                int channel;
                if (!int.TryParse(rule.Param, out channel) || channel < 1)
                {
                    return;
                }
                HashSet<KeyValuePair<AppearanceManager.WearableData, WearableType>> wearables =
                    new HashSet<KeyValuePair<AppearanceManager.WearableData, WearableType>>(
                        GetWearables(Client.Inventory.Store.RootNode));
                StringBuilder response = new StringBuilder();
                switch (!string.IsNullOrEmpty(rule.Option))
                {
                    case true:
                        RLVWearable RLVwearable = RLVWearables.AsParallel()
                            .FirstOrDefault(
                                o => o.Name.Equals(rule.Option, StringComparison.InvariantCultureIgnoreCase));
                        switch (!RLVwearable.Equals(default(RLVWearable)))
                        {
                            case true:
                                if (wearables.AsParallel().Any(o => o.Value.Equals(RLVwearable.WearableType)))
                                {
                                    response.Append(RLV_CONSTANTS.TRUE_MARKER);
                                    break;
                                }
                                goto default;
                            default:
                                response.Append(RLV_CONSTANTS.FALSE_MARKER);
                                break;
                        }
                        break;
                    default:
                        string[] data = new string[RLVWearables.Count];
                        Parallel.ForEach(Enumerable.Range(0, RLVWearables.Count), o =>
                        {
                            if (!wearables.AsParallel().Any(p => p.Value.Equals(RLVWearables[o].WearableType)))
                            {
                                data[o] = RLV_CONSTANTS.FALSE_MARKER;
                                return;
                            }
                            data[o] = RLV_CONSTANTS.TRUE_MARKER;
                        });
                        response.Append(string.Join("", data.ToArray()));
                        break;
                }
                Client.Self.Chat(response.ToString(), channel, ChatType.Normal);
            };
        }
    }
}