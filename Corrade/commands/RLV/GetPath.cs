///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
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
            public static readonly Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> getpath =
                (message, rule, senderUUID) =>
                {
                    int channel;
                    if (!int.TryParse(rule.Param, NumberStyles.Integer, Utils.EnUsCulture, out channel) || channel < 1)
                    {
                        return;
                    }
                    var RLVFolder = Inventory.FindInventory<InventoryFolder>(Client,
                        wasOpenMetaverse.RLV.RLV_CONSTANTS.SHARED_FOLDER_PATH,
                        wasOpenMetaverse.RLV.RLV_CONSTANTS.PATH_SEPARATOR, null, corradeConfiguration.ServicesTimeout,
                        Client.Inventory.Store.RootFolder);
                    if (RLVFolder == null)
                    {
                        Locks.ClientInstanceSelfLock.EnterWriteLock();
                        Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                        Locks.ClientInstanceSelfLock.ExitWriteLock();
                        return;
                    }
                    // General variables
                    InventoryBase inventoryBase = null;
                    KeyValuePair<Primitive, AttachmentPoint> attachment;
                    switch (!string.IsNullOrEmpty(rule.Option))
                    {
                        case true:
                            // Try attachments
                            var RLVattachment = wasOpenMetaverse.RLV.RLVAttachments.AsParallel().FirstOrDefault(
                                o =>
                                    string.Equals(rule.Option, o.Name,
                                        StringComparison.InvariantCultureIgnoreCase));
                            if (!RLVattachment.Equals(default(wasOpenMetaverse.RLV.RLVAttachment)))
                            {
                                attachment =
                                    Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                                        .AsParallel()
                                        .FirstOrDefault(o => o.Value.Equals(RLVattachment.AttachmentPoint));
                                switch (!attachment.Equals(default(KeyValuePair<Primitive, AttachmentPoint>)))
                                {
                                    case true:
                                        Locks.ClientInstanceInventoryLock.EnterReadLock();
                                        if (Client.Inventory.Store.Contains(attachment.Key.Properties.ItemID))
                                        {
                                            inventoryBase = Client.Inventory.Store[attachment.Key.Properties.ItemID];
                                        }
                                        Locks.ClientInstanceInventoryLock.ExitReadLock();
                                        break;

                                    default:
                                        return;
                                }
                                break;
                            }
                            var RLVwearable = wasOpenMetaverse.RLV.RLVWearables.AsParallel().FirstOrDefault(
                                o =>
                                    string.Equals(rule.Option, o.Name,
                                        StringComparison.InvariantCultureIgnoreCase));
                            if (!RLVwearable.Equals(default(wasOpenMetaverse.RLV.RLVWearable)))
                            {
                                var wearTypeInfo = typeof(WearableType).GetFields(BindingFlags.Public |
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
                                    Inventory.GetWearables(Client, RLVFolder, corradeConfiguration.ServicesTimeout)
                                        .AsParallel()
                                        .FirstOrDefault(
                                            o =>
                                                o.Equals(
                                                    (WearableType)wearTypeInfo.GetValue(null)));
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
                                    Locks.ClientInstanceInventoryLock.EnterReadLock();
                                    if (Client.Inventory.Store.Contains(attachment.Key.Properties.ItemID))
                                    {
                                        inventoryBase = Client.Inventory.Store[attachment.Key.Properties.ItemID];
                                    }
                                    Locks.ClientInstanceInventoryLock.ExitReadLock();
                                    break;
                            }
                            break;
                    }
                    if (inventoryBase == null)
                    {
                        Locks.ClientInstanceSelfLock.EnterWriteLock();
                        Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                        Locks.ClientInstanceSelfLock.ExitWriteLock();
                        return;
                    }
                    var path = inventoryBase.GetInventoryPath(Client, RLVFolder,
                        wasOpenMetaverse.RLV.RLV_CONSTANTS.PATH_SEPARATOR);
                    switch (string.IsNullOrEmpty(path))
                    {
                        case true:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        default:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.Chat(path, channel, ChatType.Normal);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;
                    }
                };
        }
    }
}
