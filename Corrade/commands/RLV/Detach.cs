///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

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
            public static Action<string, RLVRule, UUID> detach = (message, rule, senderUUID) =>
            {
                if (!rule.Param.Equals(RLV_CONSTANTS.FORCE))
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
                InventoryBase inventoryBase;
                switch (!string.IsNullOrEmpty(rule.Option))
                {
                    case true:
                        RLVAttachment RLVattachment =
                            RLVAttachments.AsParallel().FirstOrDefault(
                                o => o.Name.Equals(rule.Option, StringComparison.InvariantCultureIgnoreCase));
                        switch (!RLVattachment.Equals(default(RLVAttachment)))
                        {
                            case true: // detach by attachment point
                                Parallel.ForEach(
                                    GetAttachments(corradeConfiguration.ServicesTimeout,
                                        corradeConfiguration.DataTimeout)
                                        .AsParallel().Where(o => o.Value.Equals(RLVattachment.AttachmentPoint)),
                                    o =>
                                    {
                                        inventoryBase =
                                            FindInventory<InventoryBase>(Client.Inventory.Store.RootNode,
                                                o.Key.Properties.Name
                                                )
                                                .AsParallel().FirstOrDefault(
                                                    p =>
                                                        (p is InventoryItem) &&
                                                        ((InventoryItem) p).AssetType.Equals(
                                                            AssetType.Object));
                                        if (inventoryBase is InventoryAttachment ||
                                            inventoryBase is InventoryObject)
                                        {
                                            Detach(inventoryBase as InventoryItem);
                                        }
                                    });
                                break;
                            default: // detach by folder(s) name
                                Parallel.ForEach(
                                    rule.Option.Split(RLV_CONSTANTS.PATH_SEPARATOR[0])
                                        .AsParallel().Select(
                                            folder =>
                                                FindInventory<InventoryBase>(RLVFolder,
                                                    new Regex(Regex.Escape(folder),
                                                        RegexOptions.Compiled | RegexOptions.IgnoreCase)
                                                    ).AsParallel().FirstOrDefault(o => (o is InventoryFolder))),
                                    o =>
                                    {
                                        if (o != null)
                                        {
                                            Client.Inventory.Store.GetContents(
                                                o as InventoryFolder).FindAll(CanBeWorn)
                                                .ForEach(
                                                    p =>
                                                    {
                                                        if (p is InventoryWearable)
                                                        {
                                                            UnWear(p as InventoryItem);
                                                            return;
                                                        }
                                                        if (p is InventoryAttachment ||
                                                            p is InventoryObject)
                                                        {
                                                            // Multiple attachment points not working in libOpenMetaverse, so just replace.
                                                            Detach(p as InventoryItem);
                                                        }
                                                    });
                                        }
                                    });
                                break;
                        }
                        break;
                    default: //detach everything from RLV attachmentpoints
                        Parallel.ForEach(
                            GetAttachments(corradeConfiguration.ServicesTimeout,
                                corradeConfiguration.DataTimeout)
                                .AsParallel()
                                .Where(o => RLVAttachments.Any(p => p.AttachmentPoint.Equals(o.Value))), o =>
                                {
                                    inventoryBase = FindInventory<InventoryBase>(
                                        Client.Inventory.Store.RootNode, o.Key.Properties.Name
                                        )
                                        .AsParallel().FirstOrDefault(
                                            p =>
                                                p is InventoryItem &&
                                                ((InventoryItem) p).AssetType.Equals(AssetType.Object));
                                    if (inventoryBase is InventoryAttachment || inventoryBase is InventoryObject)
                                    {
                                        Detach(inventoryBase as InventoryItem);
                                    }
                                });
                        break;
                }
                RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
            };
        }
    }
}