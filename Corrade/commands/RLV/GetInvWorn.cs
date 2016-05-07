///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using OpenMetaverse;
using wasOpenMetaverse;
using Inventory = wasOpenMetaverse.Inventory;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> getinvworn = (message, rule, senderUUID) =>
            {
                int channel;
                if (!int.TryParse(rule.Param, out channel) || channel < 1)
                {
                    return;
                }
                InventoryNode RLVFolder =
                    Inventory.FindInventory<InventoryNode>(Client, Client.Inventory.Store.RootNode,
                        RLV_CONSTANTS.SHARED_FOLDER_NAME)
                        .ToArray()
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
                KeyValuePair<InventoryNode, LinkedList<string>> folderPath = Inventory.FindInventoryPath<InventoryNode>(
                    Client,
                    RLVFolder,
                    CORRADE_CONSTANTS.OneOrMoRegex,
                    new LinkedList<string>())
                    .ToArray()
                    .AsParallel().Where(o => o.Key.Data is InventoryFolder)
                    .FirstOrDefault(
                        o =>
                            string.Join(RLV_CONSTANTS.PATH_SEPARATOR, o.Value.Skip(1).ToArray())
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

                HashSet<InventoryItem> currentWearables =
                    new HashSet<InventoryItem>(Inventory.GetWearables(Client, CurrentOutfitFolder));
                Dictionary<Primitive, AttachmentPoint> currentAttachments =
                    Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                        .ToDictionary(o => o.Key, p => p.Value);

                Func<InventoryNode, string> GetWornIndicator = node =>
                {
                    int myItemsCount = 0;
                    int myItemsWornCount = 0;

                    Parallel.ForEach(
                        node.Nodes.Values.AsParallel().Where(
                            n =>
                                !n.Data.Name.StartsWith(RLV_CONSTANTS.DOT_MARKER) &&
                                n.Data is InventoryItem && Inventory.CanBeWorn(n.Data)
                            ), n =>
                            {
                                Interlocked.Increment(ref myItemsCount);
                                if ((n.Data is InventoryWearable &&
                                     currentWearables.AsParallel().Any(
                                         o =>
                                             o.UUID.Equals(
                                                 Inventory.ResolveItemLink(Client, n.Data as InventoryItem).UUID))) ||
                                    currentAttachments.AsParallel().Any(
                                        o =>
                                            o.Key.Properties.ItemID.Equals(
                                                Inventory.ResolveItemLink(Client, n.Data as InventoryItem).UUID)))
                                {
                                    Interlocked.Increment(ref myItemsWornCount);
                                }
                            });


                    int allItemsCount = 0;
                    int allItemsWornCount = 0;

                    Parallel.ForEach(
                        node.Nodes.Values.AsParallel().Where(
                            n =>
                                !n.Data.Name.StartsWith(RLV_CONSTANTS.DOT_MARKER) &&
                                n.Data is InventoryFolder
                            ),
                        n => Parallel.ForEach(n.Nodes.Values
                            .AsParallel().Where(o => !o.Data.Name.StartsWith(RLV_CONSTANTS.DOT_MARKER))
                            .Where(
                                o =>
                                    !o.Data.Name.StartsWith(RLV_CONSTANTS.DOT_MARKER) && o.Data is InventoryItem &&
                                    Inventory.CanBeWorn(o.Data)), p =>
                                    {
                                        Interlocked.Increment(ref allItemsCount);
                                        if ((p.Data is InventoryWearable &&
                                             currentWearables.AsParallel().Any(
                                                 o =>
                                                     o.UUID.Equals(
                                                         Inventory.ResolveItemLink(Client, p.Data as InventoryItem).UUID))) ||
                                            currentAttachments.AsParallel().Any(
                                                o =>
                                                    o.Key.Properties.ItemID.Equals(
                                                        Inventory.ResolveItemLink(Client, p.Data as InventoryItem).UUID)))
                                        {
                                            Interlocked.Increment(ref allItemsWornCount);
                                        }
                                    }));


                    Func<int, int, string> WornIndicator =
                        (all, one) => all > 0 ? (all.Equals(one) ? "3" : (one > 0 ? "2" : "1")) : "0";

                    return WornIndicator(myItemsCount, myItemsWornCount) +
                           WornIndicator(allItemsCount, allItemsWornCount);
                };
                List<string> response = new List<string>();
                lock (Locks.ClientInstanceInventoryLock)
                {
                    response.Add($"{RLV_CONSTANTS.PROPORTION_SEPARATOR}{GetWornIndicator(folderPath.Key)}");
                    response.AddRange(
                        folderPath.Key.Nodes.Values.AsParallel().Where(o => o.Data is InventoryFolder)
                            .Select(
                                o =>
                                    $"{o.Data.Name}{RLV_CONSTANTS.PROPORTION_SEPARATOR}{GetWornIndicator(o)}"));
                }
                lock (Locks.ClientInstanceSelfLock)
                {
                    Client.Self.Chat(string.Join(RLV_CONSTANTS.CSV_DELIMITER, response.ToArray()),
                        channel,
                        ChatType.Normal);
                }
            };
        }
    }
}