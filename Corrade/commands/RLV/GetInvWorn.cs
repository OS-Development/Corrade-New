///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using OpenMetaverse;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading;
using wasOpenMetaverse;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static readonly Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> getinvworn =
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
                        lock (Locks.ClientInstanceSelfLock)
                        {
                            Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                        }
                        return;
                    }

                    var currentWearables = new HashSet<UUID>();
                    var currentAttachments = new HashSet<UUID>();

                    Func<InventoryNode, string> GetWornIndicator = node =>
                    {
                        var myItemsCount = 0;
                        var myItemsWornCount = 0;

                        node.Nodes.Values.Where(
                            n =>
                                Inventory.CanBeWorn(n.Data) &&
                                !n.Data.Name.StartsWith(wasOpenMetaverse.RLV.RLV_CONSTANTS.DOT_MARKER))
                            .AsParallel()
                            .ForAll(
                                n =>
                                {
                                    Interlocked.Increment(ref myItemsCount);
                                    if ((n.Data is InventoryWearable && currentWearables.Contains(n.Data.UUID)) ||
                                        currentAttachments.Contains(n.Data.UUID))
                                    {
                                        Interlocked.Increment(ref myItemsWornCount);
                                    }
                                });

                        var allItemsCount = 0;
                        var allItemsWornCount = 0;

                        node.Nodes.Values.Where(
                            n =>
                                n.Data is InventoryFolder &&
                                !n.Data.Name.StartsWith(wasOpenMetaverse.RLV.RLV_CONSTANTS.DOT_MARKER))
                            .SelectMany(o => o.GetInventoryItems())
                            .Where(
                                n =>
                                    Inventory.CanBeWorn(n) &&
                                    !n.Name.StartsWith(wasOpenMetaverse.RLV.RLV_CONSTANTS.DOT_MARKER))
                            .AsParallel()
                            .ForAll(
                                n =>
                                {
                                    Interlocked.Increment(ref allItemsCount);
                                    if ((n is InventoryWearable && currentWearables.Contains(n.UUID)) ||
                                        currentAttachments.Contains(n.UUID))
                                    {
                                        Interlocked.Increment(ref allItemsWornCount);
                                    }
                                });

                        Func<int, int, string> WornIndicator =
                            (all, one) => all > 0 ? (all.Equals(one) ? "3" : (one > 0 ? "2" : "1")) : "0";

                        return WornIndicator(myItemsCount, myItemsWornCount) +
                               WornIndicator(allItemsCount, allItemsWornCount);
                    };

                    string[] response = null;
                    lock (RLVInventoryLock)
                    {
                        InventoryNode optionNode;
                        switch (string.IsNullOrEmpty(rule.Option))
                        {
                            case true:
                                Locks.ClientInstanceInventoryLock.EnterReadLock();
                                optionNode = Client.Inventory.Store.GetNodeFor(RLVFolder.UUID);
                                Locks.ClientInstanceInventoryLock.ExitReadLock();
                                break;

                            default:
                                optionNode = Inventory.FindInventory<InventoryNode>(Client, rule.Option,
                                    wasOpenMetaverse.RLV.RLV_CONSTANTS.PATH_SEPARATOR, null,
                                    corradeConfiguration.ServicesTimeout,
                                    RLVFolder, StringComparison.OrdinalIgnoreCase);
                                break;
                        }

                        currentWearables.UnionWith(Inventory.GetWearables(Client, CurrentOutfitFolder,
                            corradeConfiguration.ServicesTimeout)
                            .Select(o => o.UUID));
                        currentAttachments.UnionWith(Inventory.GetAttachments(Client,
                            corradeConfiguration.DataTimeout)
                            .Select(o => o.Key.Properties.ItemID));

                        Locks.ClientInstanceInventoryLock.EnterReadLock();
                        if (optionNode != null)
                        {
                            response = new string[optionNode.Nodes.Values.Count + 1];
                            response[0] =
                                $"{wasOpenMetaverse.RLV.RLV_CONSTANTS.PROPORTION_SEPARATOR}{GetWornIndicator(optionNode)}";
                            optionNode.Nodes.Values.Select((node, index) => new { node, index = index + 1 })
                                .AsParallel()
                                .Where(
                                    o =>
                                        o.node.Data is InventoryFolder &&
                                        !o.node.Data.Name.StartsWith(wasOpenMetaverse.RLV.RLV_CONSTANTS.DOT_MARKER))
                                .ForAll(o =>
                                {
                                    response[o.index] =
                                        $"{o.node.Data.Name}{wasOpenMetaverse.RLV.RLV_CONSTANTS.PROPORTION_SEPARATOR}{GetWornIndicator(o.node)}";
                                });
                        }
                        Locks.ClientInstanceInventoryLock.ExitReadLock();

                        lock (Locks.ClientInstanceSelfLock)
                        {
                            Client.Self.Chat(
                                response != null
                                    ? string.Join(wasOpenMetaverse.RLV.RLV_CONSTANTS.CSV_DELIMITER, response)
                                    : string.Empty,
                                channel,
                                ChatType.Normal);
                        }
                    }
                };
        }
    }
}
