///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

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
            public static Action<string, RLVRule, UUID> getpath = (message, rule, senderUUID) =>
            {
                int channel;
                if (!int.TryParse(rule.Param, out channel) || channel < 1)
                {
                    return;
                }
                InventoryNode RLVFolder =
                    FindInventory<InventoryNode>(Client.Inventory.Store.RootNode,
                        RLV_CONSTANTS.SHARED_FOLDER_NAME)
                        .AsParallel()
                        .FirstOrDefault(o => o.Data is InventoryFolder);
                if (RLVFolder == null)
                {
                    Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                    return;
                }
                // General variables
                InventoryBase inventoryBase = null;
                KeyValuePair<Primitive, AttachmentPoint> attachment;
                switch (!string.IsNullOrEmpty(rule.Option))
                {
                    case true:
                        // Try attachments
                        RLVAttachment RLVattachment =
                            RLVAttachments.AsParallel().FirstOrDefault(
                                o => o.Name.Equals(rule.Option, StringComparison.InvariantCultureIgnoreCase));
                        if (!RLVattachment.Equals(default(RLVAttachment)))
                        {
                            attachment =
                                GetAttachments(corradeConfiguration.DataTimeout)
                                    .AsParallel()
                                    .FirstOrDefault(o => o.Value.Equals(RLVattachment.AttachmentPoint));
                            switch (!attachment.Equals(default(KeyValuePair<Primitive, AttachmentPoint>)))
                            {
                                case true:
                                    inventoryBase = FindInventory<InventoryBase>(
                                        RLVFolder, attachment.Key.Properties.ItemID
                                        )
                                        .AsParallel().FirstOrDefault(
                                            p =>
                                                (p is InventoryItem) &&
                                                ((InventoryItem) p).AssetType.Equals(AssetType.Object));
                                    break;
                                default:
                                    return;
                            }
                            break;
                        }
                        RLVWearable RLVwearable =
                            RLVWearables.AsParallel().FirstOrDefault(
                                o => o.Name.Equals(rule.Option, StringComparison.InvariantCultureIgnoreCase));
                        if (!RLVwearable.Equals(default(RLVWearable)))
                        {
                            FieldInfo wearTypeInfo = typeof (WearableType).GetFields(BindingFlags.Public |
                                                                                     BindingFlags.Static)
                                .AsParallel().FirstOrDefault(
                                    p =>
                                        p.Name.Equals(rule.Option,
                                            StringComparison.InvariantCultureIgnoreCase));
                            if (wearTypeInfo == null)
                            {
                                return;
                            }
                            InventoryBase wearable =
                                GetWearables(RLVFolder.Data as InventoryFolder)
                                    .AsParallel()
                                    .FirstOrDefault(
                                        o =>
                                            (o as InventoryWearable).Equals((WearableType) wearTypeInfo.GetValue(null)));
                            if (wearable != null)
                            {
                                inventoryBase = wearable;
                            }
                        }
                        break;
                    default:
                        attachment =
                            GetAttachments(corradeConfiguration.DataTimeout)
                                .AsParallel().FirstOrDefault(o => o.Key.ID.Equals(senderUUID));
                        switch (!attachment.Equals(default(KeyValuePair<Primitive, AttachmentPoint>)))
                        {
                            case true:
                                inventoryBase = FindInventory<InventoryBase>(
                                    Client.Inventory.Store.RootNode, attachment.Key.Properties.ItemID
                                    )
                                    .AsParallel().FirstOrDefault(
                                        p =>
                                            (p is InventoryItem) &&
                                            ((InventoryItem) p).AssetType.Equals(AssetType.Object));
                                break;
                        }
                        break;
                }
                if (inventoryBase == null)
                {
                    Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                    return;
                }
                KeyValuePair<InventoryBase, LinkedList<string>> path =
                    FindInventoryPath<InventoryBase>(RLVFolder, inventoryBase.Name,
                        new LinkedList<string>()).FirstOrDefault();
                switch (!path.Equals(default(KeyValuePair<InventoryBase, LinkedList<string>>)))
                {
                    case true:
                        Client.Self.Chat(string.Join(RLV_CONSTANTS.PATH_SEPARATOR, path.Value.ToArray()),
                            channel,
                            ChatType.Normal);
                        break;
                    default:
                        Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                        return;
                }
            };
        }
    }
}