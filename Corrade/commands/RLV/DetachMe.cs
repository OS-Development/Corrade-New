using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> detachme = (message, rule, senderUUID) =>
            {
                if (!rule.Param.Equals(RLV_CONSTANTS.FORCE))
                {
                    return;
                }
                KeyValuePair<Primitive, AttachmentPoint> attachment =
                    GetAttachments(corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout)
                        .AsParallel().FirstOrDefault(o => o.Key.ID.Equals(senderUUID));
                switch (!attachment.Equals(default(KeyValuePair<Primitive, AttachmentPoint>)))
                {
                    case true:
                        InventoryBase inventoryBase =
                            FindInventory<InventoryBase>(Client.Inventory.Store.RootNode,
                                attachment.Key.Properties.ItemID
                                )
                                .AsParallel().FirstOrDefault(
                                    p =>
                                        (p is InventoryItem) &&
                                        ((InventoryItem) p).AssetType.Equals(AssetType.Object));
                        if (inventoryBase is InventoryAttachment || inventoryBase is InventoryObject)
                        {
                            Detach(inventoryBase as InventoryItem);
                        }
                        RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                        break;
                    default:
                        return;
                }
            };
        }
    }
}