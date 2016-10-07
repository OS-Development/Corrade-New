///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;
using wasOpenMetaverse;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static readonly Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> findfolder =
                (message, rule, senderUUID) =>
                {
                    int channel;
                    if (!int.TryParse(rule.Param, out channel) || channel < 1)
                    {
                        return;
                    }
                    if (string.IsNullOrEmpty(rule.Option))
                    {
                        lock (Locks.ClientInstanceSelfLock)
                        {
                            Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                        }
                        return;
                    }
                    var RLVFolder = Inventory.FindInventory<InventoryFolder>(Client,
                        wasOpenMetaverse.RLV.RLV_CONSTANTS.SHARED_FOLDER_PATH,
                        wasOpenMetaverse.RLV.RLV_CONSTANTS.PATH_SEPARATOR, null, corradeConfiguration.ServicesTimeout,
                        Client.Inventory.Store.RootFolder);
                    if (RLVFolder == null)
                    {
                        Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                        return;
                    }
                    var parts =
                        new HashSet<string>(rule.Option.Split(new[] {wasOpenMetaverse.RLV.RLV_CONSTANTS.AND_OPERATOR},
                            StringSplitOptions.RemoveEmptyEntries));
                    var folders = RLVFolder.GetInventoryRecursive(Client, corradeConfiguration.ServicesTimeout)
                        .ToArray()
                        .AsParallel()
                        .Where(
                            o =>
                                o is InventoryFolder &&
                                !o.Name.StartsWith(wasOpenMetaverse.RLV.RLV_CONSTANTS.DOT_MARKER) &&
                                !o.Name.StartsWith(wasOpenMetaverse.RLV.RLV_CONSTANTS.TILDE_MARKER) &&
                                parts.All(p => o.Name.Contains(p))).Select(o => o.Name).ToArray();
                    if (folders.Any())
                    {
                        lock (Locks.ClientInstanceSelfLock)
                        {
                            Client.Self.Chat(
                                string.Join(wasOpenMetaverse.RLV.RLV_CONSTANTS.PATH_SEPARATOR.ToString(), folders),
                                channel,
                                ChatType.Normal);
                        }
                    }
                };
        }
    }
}