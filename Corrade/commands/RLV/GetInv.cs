///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
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
            public static Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> getinv = (message, rule, senderUUID) =>
            {
                int channel;
                if (!int.TryParse(rule.Param, out channel) || channel < 1)
                {
                    return;
                }
                var RLVFolder =
                    Inventory.FindInventory<InventoryNode>(Client, Client.Inventory.Store.RootNode,
                        wasOpenMetaverse.RLV.RLV_CONSTANTS.SHARED_FOLDER_NAME, corradeConfiguration.ServicesTimeout)
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
                InventoryNode optionFolderNode;
                switch (!string.IsNullOrEmpty(rule.Option))
                {
                    case true:
                        var folderPath = Inventory.FindInventoryPath
                            <InventoryNode>(Client,
                                RLVFolder,
                                CORRADE_CONSTANTS.OneOrMoRegex,
                                new LinkedList<string>())
                            .ToArray()
                            .AsParallel().Where(o => o.Key.Data is InventoryFolder)
                            .FirstOrDefault(
                                o =>
                                    string.Join(wasOpenMetaverse.RLV.RLV_CONSTANTS.PATH_SEPARATOR,
                                        o.Value.Skip(1).ToArray())
                                        .Equals(rule.Option, StringComparison.InvariantCultureIgnoreCase));
                        switch (!folderPath.Equals(default(KeyValuePair<InventoryNode, LinkedList<string>>)))
                        {
                            case true:
                                optionFolderNode = folderPath.Key;
                                break;
                            default:
                                lock (Locks.ClientInstanceSelfLock)
                                {
                                    Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                                }
                                return;
                        }
                        break;
                    default:
                        optionFolderNode = RLVFolder;
                        break;
                }
                var csv =
                    new HashSet<string>(Inventory.FindInventory<InventoryBase>(Client, optionFolderNode,
                        CORRADE_CONSTANTS.OneOrMoRegex, corradeConfiguration.ServicesTimeout)
                        .ToArray()
                        .AsParallel()
                        .Where(
                            o =>
                                o is InventoryFolder &&
                                !o.Name.StartsWith(wasOpenMetaverse.RLV.RLV_CONSTANTS.DOT_MARKER))
                        .Skip(1)
                        .Select(o => o.Name));
                switch (!csv.Count.Equals(0))
                {
                    case true:
                        lock (Locks.ClientInstanceSelfLock)
                        {
                            Client.Self.Chat(
                                string.Join(wasOpenMetaverse.RLV.RLV_CONSTANTS.CSV_DELIMITER, csv.ToArray()), channel,
                                ChatType.Normal);
                        }
                        break;
                    default:
                        lock (Locks.ClientInstanceSelfLock)
                        {
                            Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                        }
                        break;
                }
            };
        }
    }
}