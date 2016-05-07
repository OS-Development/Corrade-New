///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;
using wasOpenMetaverse;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> replytoinventoryoffer =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    UUID session;
                    if (
                        !UUID.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.SESSION)),
                                    corradeCommandParameters.Message)),
                            out session))
                    {
                        throw new ScriptException(ScriptError.NO_SESSION_SPECIFIED);
                    }
                    KeyValuePair<InventoryObjectOfferedEventArgs, ManualResetEvent> offer;
                    lock (InventoryOffersLock)
                    {
                        offer =
                            InventoryOffers.AsParallel()
                                .FirstOrDefault(o => o.Key.Offer.IMSessionID.Equals(session));
                    }
                    if (offer.Equals(default(KeyValuePair<InventoryObjectOfferedEventArgs, ManualResetEvent>)))
                    {
                        throw new ScriptException(ScriptError.INVENTORY_OFFER_NOT_FOUND);
                    }
                    string folder = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FOLDER)),
                            corradeCommandParameters.Message));
                    InventoryFolder inventoryFolder;
                    switch (!string.IsNullOrEmpty(folder))
                    {
                        case true:
                            UUID folderUUID;
                            if (UUID.TryParse(folder, out folderUUID))
                            {
                                InventoryBase inventoryBaseItem =
                                    Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                        folderUUID
                                        ).FirstOrDefault();
                                if (inventoryBaseItem == null)
                                {
                                    throw new ScriptException(ScriptError.FOLDER_NOT_FOUND);
                                }
                                inventoryFolder = inventoryBaseItem as InventoryFolder;
                            }
                            else
                            {
                                // attempt regex and then fall back to string
                                InventoryBase inventoryBaseItem = null;
                                try
                                {
                                    inventoryBaseItem =
                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                            new Regex(folder, RegexOptions.Compiled | RegexOptions.IgnoreCase))
                                            .FirstOrDefault();
                                }
                                catch (Exception)
                                {
                                    // not a regex so we do not care
                                    inventoryBaseItem =
                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                            folder)
                                            .FirstOrDefault();
                                }
                                if (inventoryBaseItem == null)
                                {
                                    throw new ScriptException(ScriptError.FOLDER_NOT_FOUND);
                                }
                                inventoryFolder = inventoryBaseItem as InventoryFolder;
                            }
                            if (inventoryFolder == null)
                            {
                                throw new ScriptException(ScriptError.FOLDER_NOT_FOUND);
                            }
                            break;
                        default:
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                inventoryFolder =
                                    Client.Inventory.Store.Items[Client.Inventory.FindFolderForType(AssetType.Object)]
                                        .Data as InventoryFolder;
                            }
                            break;
                    }
                    switch (
                        Reflection.GetEnumValueFromName<Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Action.ACCEPT:
                            lock (InventoryOffersLock)
                            {
                                if (!inventoryFolder.UUID.Equals(UUID.Zero))
                                {
                                    offer.Key.FolderID = inventoryFolder.UUID;
                                }
                                offer.Key.Accept = true;
                                offer.Value.Set();
                            }
                            break;
                        case Action.DECLINE:
                            lock (InventoryOffersLock)
                            {
                                offer.Key.Accept = false;
                                offer.Value.Set();
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                    // remove inventory offer
                    lock (InventoryOffersLock)
                    {
                        InventoryOffers.Remove(offer.Key);
                    }
                };
        }
    }
}