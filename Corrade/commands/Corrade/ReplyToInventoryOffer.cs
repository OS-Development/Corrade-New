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

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> replytoinventoryoffer =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Inventory))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    UUID session;
                    if (
                        !UUID.TryParse(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.SESSION)),
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
                    object folder =
                        StringOrUUID(
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.FOLDER)),
                                corradeCommandParameters.Message)));
                    InventoryFolder inventoryFolder;
                    switch (folder != null)
                    {
                        case true:
                            InventoryBase inventoryBaseItem =
                                FindInventory<InventoryBase>(Client.Inventory.Store.RootNode, folder
                                    ).FirstOrDefault();
                            if (inventoryBaseItem == null)
                            {
                                throw new ScriptException(ScriptError.FOLDER_NOT_FOUND);
                            }
                            inventoryFolder = inventoryBaseItem as InventoryFolder;
                            if (inventoryFolder == null)
                            {
                                throw new ScriptException(ScriptError.FOLDER_NOT_FOUND);
                            }
                            break;
                        default:
                            inventoryFolder =
                                Client.Inventory.Store.Items[Client.Inventory.FindFolderForType(offer.Key.AssetType)
                                    ]
                                    .Data as InventoryFolder;
                            break;
                    }
                    switch (
                        wasGetEnumValueFromDescription<Action>(
                            wasInput(
                                wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ACTION)),
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