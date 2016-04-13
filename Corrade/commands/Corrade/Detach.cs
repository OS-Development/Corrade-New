///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;
using Helpers = wasOpenMetaverse.Helpers;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> detach =
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
                    CSV.ToEnumerable(
                        attachments).ToArray().AsParallel().Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                        {
                            InventoryItem inventoryItem;
                            UUID itemUUID;
                            if (UUID.TryParse(o, out itemUUID))
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
                                            new Regex(o, RegexOptions.Compiled | RegexOptions.IgnoreCase)).FirstOrDefault();
                                }
                                catch (Exception)
                                {
                                    // not a regex so we do not care
                                    inventoryBaseItem =
                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode, o)
                                            .FirstOrDefault();
                                }
                                if (inventoryBaseItem == null)
                                    return;
                                inventoryItem = inventoryBaseItem as InventoryItem;
                            }
                            if (inventoryItem == null)
                                return;

                            if (inventoryItem is InventoryObject || inventoryItem is InventoryAttachment)
                                Inventory.Detach(Client, CurrentOutfitFolder, inventoryItem,
                                corradeConfiguration.ServicesTimeout);
                        });
                    RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                };
        }
    }
}