///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> getinv = (message, rule, senderUUID) =>
            {
                int channel;
                if (!int.TryParse(rule.Param, out channel) || channel < 1)
                {
                    return;
                }
                if (string.IsNullOrEmpty(rule.Option))
                {
                    Client.Self.Chat(string.Empty, channel, ChatType.Normal);
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
                InventoryNode optionFolderNode;
                switch (!string.IsNullOrEmpty(rule.Option))
                {
                    case true:
                        KeyValuePair<InventoryNode, LinkedList<string>> folderPath = FindInventoryPath
                            <InventoryNode>(
                                RLVFolder,
                                CORRADE_CONSTANTS.OneOrMoRegex,
                                new LinkedList<string>())
                            .AsParallel().Where(o => o.Key.Data is InventoryFolder)
                            .FirstOrDefault(
                                o =>
                                    string.Join(RLV_CONSTANTS.PATH_SEPARATOR, o.Value.Skip(1).ToArray())
                                        .Equals(rule.Option, StringComparison.InvariantCultureIgnoreCase));
                        switch (!folderPath.Equals(default(KeyValuePair<InventoryNode, LinkedList<string>>)))
                        {
                            case true:
                                optionFolderNode = folderPath.Key;
                                break;
                            default:
                                Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                                return;
                        }
                        break;
                    default:
                        optionFolderNode = RLVFolder;
                        break;
                }
                HashSet<string> csv = new HashSet<string>();
                object LockObject = new object();
                Parallel.ForEach(
                    FindInventory<InventoryBase>(optionFolderNode, CORRADE_CONSTANTS.OneOrMoRegex),
                    o =>
                    {
                        if (o.Name.StartsWith(RLV_CONSTANTS.DOT_MARKER)) return;
                        lock (LockObject)
                        {
                            csv.Add(o.Name);
                        }
                    });
                Client.Self.Chat(string.Join(RLV_CONSTANTS.CSV_DELIMITER, csv.ToArray()), channel,
                    ChatType.Normal);
            };
        }
    }
}