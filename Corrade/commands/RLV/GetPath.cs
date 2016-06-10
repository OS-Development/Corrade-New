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
using wasOpenMetaverse;
using Inventory = wasOpenMetaverse.Inventory;

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
                var RLVFolder =
                    Inventory.FindInventory<InventoryNode>(Client, Client.Inventory.Store.RootNode,
                        RLV_CONSTANTS.SHARED_FOLDER_NAME)
                        .AsParallel()
                        .FirstOrDefault(o => o.Data is InventoryFolder);
                if (RLVFolder == null)
                {
                    lock (Locks.ClientInstanceSelfLock)
                    {
                        Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                    }
                    return;
                }
                // General variables
                InventoryBase inventoryBase = null;
                KeyValuePair<Primitive, AttachmentPoint> attachment;
                switch (!string.IsNullOrEmpty(rule.Option))
                {
                    case true:
                        // Try attachments
                        var RLVattachment =
                            RLVAttachments.AsParallel().FirstOrDefault(
                                o => string.Equals(rule.Option, o.Name, StringComparison.InvariantCultureIgnoreCase));
                        if (!RLVattachment.Equals(default(RLVAttachment)))
                        {
                            attachment =
                                Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                                    .AsParallel()
                                    .FirstOrDefault(o => o.Value.Equals(RLVattachment.AttachmentPoint));
                            switch (!attachment.Equals(default(KeyValuePair<Primitive, AttachmentPoint>)))
                            {
                                case true:
                                    inventoryBase = Inventory.FindInventory<InventoryBase>(Client,
                                        RLVFolder, attachment.Key.Properties.ItemID
                                        )
                                        .AsParallel().FirstOrDefault(
                                            p =>
                                                p is InventoryItem &&
                                                ((InventoryItem) p).AssetType.Equals(AssetType.Object));
                                    break;
                                default:
                                    return;
                            }
                            break;
                        }
                        var RLVwearable =
                            RLVWearables.AsParallel().FirstOrDefault(
                                o => string.Equals(rule.Option, o.Name, StringComparison.InvariantCultureIgnoreCase));
                        if (!RLVwearable.Equals(default(RLVWearable)))
                        {
                            var wearTypeInfo = typeof (WearableType).GetFields(BindingFlags.Public |
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
                                Inventory.GetWearables(Client, RLVFolder.Data as InventoryFolder)
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
                            Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                                .AsParallel().FirstOrDefault(o => o.Key.ID.Equals(senderUUID));
                        switch (!attachment.Equals(default(KeyValuePair<Primitive, AttachmentPoint>)))
                        {
                            case true:
                                inventoryBase = Inventory.FindInventory<InventoryBase>(Client,
                                    Client.Inventory.Store.RootNode, attachment.Key.Properties.ItemID
                                    )
                                    .AsParallel().FirstOrDefault(
                                        p =>
                                            p is InventoryItem &&
                                            ((InventoryItem) p).AssetType.Equals(AssetType.Object));
                                break;
                        }
                        break;
                }
                if (inventoryBase == null)
                {
                    lock (Locks.ClientInstanceSelfLock)
                    {
                        Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                    }
                    return;
                }
                var path =
                    Inventory.FindInventoryPath<InventoryBase>(Client, RLVFolder, inventoryBase.Name,
                        new LinkedList<string>()).FirstOrDefault();
                switch (!path.Equals(default(KeyValuePair<InventoryBase, LinkedList<string>>)))
                {
                    case true:
                        lock (Locks.ClientInstanceSelfLock)
                        {
                            Client.Self.Chat(string.Join(RLV_CONSTANTS.PATH_SEPARATOR, path.Value.ToArray()),
                                channel,
                                ChatType.Normal);
                        }
                        break;
                    default:
                        lock (Locks.ClientInstanceSelfLock)
                        {
                            Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                        }
                        return;
                }
            };
        }
    }
}