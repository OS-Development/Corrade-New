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
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> findfolder = (message, rule, senderUUID) =>
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
                List<string> folders = new List<string>();
                HashSet<string> parts =
                    new HashSet<string>(rule.Option.Split(RLV_CONSTANTS.AND_OPERATOR.ToCharArray()));
                object LockObject = new object();
                Parallel.ForEach(FindInventoryPath<InventoryBase>(RLVFolder,
                    CORRADE_CONSTANTS.OneOrMoRegex,
                    new LinkedList<string>())
                    .AsParallel().Where(
                        o =>
                            o.Key is InventoryFolder &&
                            !o.Key.Name.Substring(1).Equals(RLV_CONSTANTS.DOT_MARKER) &&
                            !o.Key.Name.Substring(1).Equals(RLV_CONSTANTS.TILDE_MARKER)), o =>
                            {
                                int count = 0;
                                Parallel.ForEach(parts, p => Parallel.ForEach(o.Value, q =>
                                {
                                    if (q.Contains(p))
                                    {
                                        Interlocked.Increment(ref count);
                                    }
                                }));
                                if (!count.Equals(parts.Count)) return;
                                lock (LockObject)
                                {
                                    folders.Add(o.Key.Name);
                                }
                            });
                if (folders.Any())
                {
                    Client.Self.Chat(string.Join(RLV_CONSTANTS.PATH_SEPARATOR, folders.ToArray()),
                        channel,
                        ChatType.Normal);
                }
            };
        }
    }
}