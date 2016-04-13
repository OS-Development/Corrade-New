///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Helpers = wasOpenMetaverse.Helpers;
using Inventory = wasOpenMetaverse.Inventory;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> attach =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string attachments =
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ATTACHMENTS)),
                                corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(attachments))
                    {
                        throw new ScriptException(ScriptError.EMPTY_ATTACHMENTS);
                    }
                    bool replace;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.REPLACE)),
                                    corradeCommandParameters.Message)),
                            out replace))
                    {
                        replace = true;
                    }
                    Dictionary<string, string> items = CSV.ToKeyValue(attachments)
                        .ToDictionary(o => o.Key, o => o.Value);
                    // if this is SecondLife, check that the additional attachments would not exceed the maximum attachment limit
                    if (Helpers.IsSecondLife(Client))
                    {
                        switch (replace)
                        {
                            case true:
                                if (
                                    Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                                        .Count() + items.Count() -
                                    typeof (AttachmentPoint).GetFields(
                                        BindingFlags.Public | BindingFlags.Static)
                                        .AsParallel().Count(p => !items.ContainsKey(p.Name)) >
                                    Constants.AVATARS.MAXIMUM_NUMBER_OF_ATTACHMENTS)
                                {
                                    throw new Exception(
                                        Reflection.GetNameFromEnumValue(
                                            ScriptError.ATTACHMENTS_WOULD_EXCEED_MAXIMUM_ATTACHMENT_LIMIT));
                                }
                                break;
                            default:
                                if (items.Count +
                                    Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                                        .Count() >
                                    Constants.AVATARS.MAXIMUM_NUMBER_OF_ATTACHMENTS)
                                {
                                    throw new Exception(
                                        Reflection.GetNameFromEnumValue(
                                            ScriptError.ATTACHMENTS_WOULD_EXCEED_MAXIMUM_ATTACHMENT_LIMIT));
                                }
                                break;
                        }
                    }
                    Parallel.ForEach(items, o =>
                        typeof (AttachmentPoint).GetFields(BindingFlags.Public | BindingFlags.Static)
                            .AsParallel().Where(
                                p =>
                                    string.Equals(o.Key, p.Name, StringComparison.Ordinal)).ForAll(
                                        q =>
                                        {
                                            InventoryItem inventoryItem;
                                            UUID itemUUID;
                                            if (UUID.TryParse(o.Value, out itemUUID))
                                            {
                                                InventoryBase inventoryBaseItem =
                                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode, itemUUID
                                                            ).FirstOrDefault();
                                                if (inventoryBaseItem == null)
                                                    return;
                                                inventoryItem = inventoryBaseItem as InventoryItem;
                                            }
                                            else
                                            {
                                                // attempt regex and then fall back to string
                                                InventoryBase inventoryBaseItem = null;
                                                try
                                                {
                                                    inventoryBaseItem =
                                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                                            new Regex(o.Value, RegexOptions.Compiled | RegexOptions.IgnoreCase)).FirstOrDefault();
                                                }
                                                catch (Exception)
                                                {
                                                    // not a regex so we do not care
                                                    inventoryBaseItem =
                                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode, o.Value)
                                                            .FirstOrDefault();
                                                }
                                                if (inventoryBaseItem == null)
                                                    return;
                                                inventoryItem = inventoryBaseItem as InventoryItem;
                                            }
                                            if (inventoryItem == null)
                                                return;

                                            if (inventoryItem is InventoryObject || inventoryItem is InventoryAttachment)
                                                Inventory.Attach(Client, CurrentOutfitFolder,
                                                    inventoryItem,
                                                    (AttachmentPoint) q.GetValue(null),
                                                    replace, corradeConfiguration.ServicesTimeout);

                                        }));
                    RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                };
        }
    }
}