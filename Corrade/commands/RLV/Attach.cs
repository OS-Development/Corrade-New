using System;
using System.Linq;
using System.Text.RegularExpressions;
using OpenMetaverse;

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
                            folder =>
                                FindInventory<InventoryBase>(RLVFolder,
                                    new Regex(Regex.Escape(folder),
                                        RegexOptions.Compiled | RegexOptions.IgnoreCase)
                                    ).AsParallel().FirstOrDefault(o => (o is InventoryFolder))), o =>
                                    {
                                        if (o != null)
                                        {
                                            Client.Inventory.Store.GetContents(o as InventoryFolder).
                                                FindAll(CanBeWorn)
                                                .ForEach(
                                                    p =>
                                                    {
                                                        if (p is InventoryWearable)
                                                        {
                                                            Wear(p as InventoryItem, true);
                                                            return;
                                                        }
                                                        if (p is InventoryObject || p is InventoryAttachment)
                                                        {
                                                            // Multiple attachment points not working in libOpenMetaverse, so just replace.
                                                            Attach(p as InventoryItem,
                                                                AttachmentPoint.Default,
                                                                true);
                                                        }
                                                    });
                                        }
                                    });
                RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
            };
        }
    }
}