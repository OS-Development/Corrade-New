///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
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
            public static readonly Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> getinv =
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
                    InventoryFolder optionFolder;
                    switch (string.IsNullOrEmpty(rule.Option))
                    {
                        case true:
                            optionFolder = RLVFolder;
                            break;

                        default:
                            optionFolder = Inventory.FindInventory<InventoryFolder>(Client, rule.Option,
                                wasOpenMetaverse.RLV.RLV_CONSTANTS.PATH_SEPARATOR, null,
                                corradeConfiguration.ServicesTimeout,
                                RLVFolder,
                                StringComparison.OrdinalIgnoreCase);
                            if (optionFolder == null)
                            {
                                Locks.ClientInstanceSelfLock.EnterWriteLock();
                                Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                                Locks.ClientInstanceSelfLock.ExitWriteLock();
                                return;
                            }
                            break;
                    }
                    var csv = new HashSet<string>();
                    csv.UnionWith(Inventory.FolderContents(Client, optionFolder.UUID, optionFolder.UUID, true, false,
                        InventorySortOrder.ByDate, (int)corradeConfiguration.ServicesTimeout).AsParallel()
                        .Where(
                            o =>
                                o is InventoryFolder &&
                                !o.Name.StartsWith(wasOpenMetaverse.RLV.RLV_CONSTANTS.DOT_MARKER))
                        .Select(o => o.Name));
                    switch (csv.Any())
                    {
                        case true:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.Chat(
                                    string.Join(wasOpenMetaverse.RLV.RLV_CONSTANTS.CSV_DELIMITER, csv),
                                    channel,
                                    ChatType.Normal);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        default:
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.Chat(string.Empty, channel, ChatType.Normal);
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;
                    }
                };
        }
    }
}
