///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

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
            public static Action<string, RLVRule, UUID> getinv = (message, rule, senderUUID) =>
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
                HashSet<string> csv =
                    new HashSet<string>(FindInventory<InventoryBase>(optionFolderNode, CORRADE_CONSTANTS.OneOrMoRegex)
                        .AsParallel()
                        .Where(o => o is InventoryFolder && !o.Name.StartsWith(RLV_CONSTANTS.DOT_MARKER))
                        .Skip(1)
                        .Select(o => o.Name));
                switch (!csv.Count.Equals(0))
                {
                    case true:
                        Client.Self.Chat(string.Join(RLV_CONSTANTS.CSV_DELIMITER, csv.ToArray()), channel,
                            ChatType.Normal);
                        break;
                    default:
                        Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                        break;
                }
            };
        }
    }
}