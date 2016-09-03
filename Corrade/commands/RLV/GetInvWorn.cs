///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Corrade.Constants;
using OpenMetaverse;
using wasOpenMetaverse;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> getinvworn = (message, rule, senderUUID) =>
            {
                int channel;
                if (!int.TryParse(rule.Param, out channel) || channel < 1)
                {
                    return;
                }
                var RLVFolder =
                    Inventory.FindInventory<InventoryNode>(Client, Client.Inventory.Store.RootNode,
                        wasOpenMetaverse.RLV.RLV_CONSTANTS.SHARED_FOLDER_NAME, corradeConfiguration.ServicesTimeout)
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
                var folderPath = Inventory.FindInventoryPath<InventoryNode>(
                    Client,
                    RLVFolder,
                    CORRADE_CONSTANTS.OneOrMoRegex,
                    new LinkedList<string>())
                    .AsParallel().Where(o => o.Key.Data is InventoryFolder)
                    .FirstOrDefault(
                        o =>
                            string.Join(wasOpenMetaverse.RLV.RLV_CONSTANTS.PATH_SEPARATOR, o.Value.Skip(1).ToArray())
                                .Equals(rule.Option, StringComparison.InvariantCultureIgnoreCase));
                switch (!folderPath.Equals(default(KeyValuePair<InventoryNode, LinkedList<string>>)))
                {
                    case false:
                        lock (Locks.ClientInstanceSelfLock)
                        {
                            Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                        }
                        return;
                }

                var currentWearables =
                    new HashSet<UUID>(Inventory.GetWearables(Client, CurrentOutfitFolder).Select(o => o.UUID));
                var currentAttachments = new HashSet<UUID>(
                    Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                        .Select(o => o.Key.Properties.ItemID));

                Func<InventoryNode, string> GetWornIndicator = node =>
                {
                    var myItemsCount = 0;
                    var myItemsWornCount = 0;

                    node.Nodes.Values.AsParallel().Where(
                        n =>
                            !n.Data.Name.StartsWith(wasOpenMetaverse.RLV.RLV_CONSTANTS.DOT_MARKER) &&
                            n.Data is InventoryItem && Inventory.CanBeWorn(n.Data)
                        ).ForAll(n =>
                        {
                            Interlocked.Increment(ref myItemsCount);
                            var inventoryItem = Inventory.ResolveItemLink(Client, n.Data as InventoryItem);
                            if (inventoryItem == null) return;
                            var itemUUID = inventoryItem.UUID;
                            var increment = false;
                            switch (n.Data is InventoryWearable)
                            {
                                case true:
                                    if (currentWearables.Contains(itemUUID))
                                        increment = true;
                                    break;
                                default:
                                    if (currentAttachments.Contains(itemUUID))
                                        increment = true;
                                    break;
                            }

                            if (increment == false) return;

                            Interlocked.Increment(ref myItemsWornCount);
                        });


                    var allItemsCount = 0;
                    var allItemsWornCount = 0;

                    node.Nodes.Values.AsParallel().Where(
                        n =>
                            !n.Data.Name.StartsWith(wasOpenMetaverse.RLV.RLV_CONSTANTS.DOT_MARKER) &&
                            n.Data is InventoryFolder
                        ).ForAll(
                            n => n.Nodes.Values
                                .AsParallel()
                                .Where(o => !o.Data.Name.StartsWith(wasOpenMetaverse.RLV.RLV_CONSTANTS.DOT_MARKER))
                                .Where(
                                    o =>
                                        o.Data is InventoryItem && Inventory.CanBeWorn(o.Data) &&
                                        !o.Data.Name.StartsWith(wasOpenMetaverse.RLV.RLV_CONSTANTS.DOT_MARKER))
                                .ForAll(p =>
                                {
                                    Interlocked.Increment(ref allItemsCount);

                                    Interlocked.Increment(ref myItemsCount);
                                    var inventoryItem = Inventory.ResolveItemLink(Client,
                                        p.Data as InventoryItem);
                                    if (inventoryItem == null) return;
                                    var itemUUID = inventoryItem.UUID;
                                    var increment = false;
                                    switch (p.Data is InventoryWearable)
                                    {
                                        case true:
                                            if (currentWearables.Contains(itemUUID))
                                                increment = true;
                                            break;
                                        default:
                                            if (currentAttachments.Contains(itemUUID))
                                                increment = true;
                                            break;
                                    }

                                    if (increment == false) return;

                                    Interlocked.Increment(ref allItemsWornCount);
                                }));


                    Func<int, int, string> WornIndicator =
                        (all, one) => all > 0 ? (all.Equals(one) ? "3" : (one > 0 ? "2" : "1")) : "0";

                    return WornIndicator(myItemsCount, myItemsWornCount) +
                           WornIndicator(allItemsCount, allItemsWornCount);
                };

                var response = new List<string>();
                response.Add(
                    $"{wasOpenMetaverse.RLV.RLV_CONSTANTS.PROPORTION_SEPARATOR}{GetWornIndicator(folderPath.Key)}");
                response.AddRange(
                    folderPath.Key.Nodes.Values.AsParallel().Where(o => o.Data is InventoryFolder)
                        .Select(
                            o =>
                                $"{o.Data.Name}{wasOpenMetaverse.RLV.RLV_CONSTANTS.PROPORTION_SEPARATOR}{GetWornIndicator(o)}"));

                lock (Locks.ClientInstanceSelfLock)
                {
                    Client.Self.Chat(string.Join(wasOpenMetaverse.RLV.RLV_CONSTANTS.CSV_DELIMITER, response.ToArray()),
                        channel,
                        ChatType.Normal);
                }
            };
        }
    }
}