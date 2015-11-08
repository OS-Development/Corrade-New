///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Linq;
using System.Text.RegularExpressions;
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> attach = (message, rule, senderUUID) =>
            {
                if (!rule.Param.Equals(RLV_CONSTANTS.FORCE) || string.IsNullOrEmpty(rule.Option))
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
                    return;
                }
                Parallel.ForEach(
                    rule.Option.Split(RLV_CONSTANTS.PATH_SEPARATOR[0])
                        .AsParallel().Select(
                            p =>
                                FindInventory<InventoryBase>(RLVFolder,
                                    new Regex(Regex.Escape(p),
                                        RegexOptions.Compiled | RegexOptions.IgnoreCase)
                                    ).AsParallel().FirstOrDefault(o => (o is InventoryFolder))).Where(o => o != null)
                        .SelectMany(
                            o =>
                                Client.Inventory.Store.GetContents(o as InventoryFolder)
                                    .AsParallel()
                                    .Where(CanBeWorn)), o =>
                                    {
                                        if (o is InventoryWearable)
                                        {
                                            Wear(o as InventoryItem, true);
                                            return;
                                        }
                                        if (o is InventoryObject || o is InventoryAttachment)
                                        {
                                            Attach(o as InventoryItem, AttachmentPoint.Default, true);
                                        }
                                    });
                RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
            };
        }
    }
}