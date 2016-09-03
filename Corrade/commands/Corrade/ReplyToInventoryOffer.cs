///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Command.CorradeCommandParameters, Dictionary<string, string>> replytoinventoryoffer =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    UUID session;
                    if (
                        !UUID.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SESSION)),
                                    corradeCommandParameters.Message)),
                            out session))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_SESSION_SPECIFIED);
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
                        throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_OFFER_NOT_FOUND);
                    }
                    var folder = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FOLDER)),
                            corradeCommandParameters.Message));
                    InventoryFolder inventoryFolder;
                    switch (!string.IsNullOrEmpty(folder))
                    {
                        case true:
                            UUID folderUUID;
                            switch (UUID.TryParse(folder, out folderUUID))
                            {
                                case true:
                                    inventoryFolder =
                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                            folderUUID, corradeConfiguration.ServicesTimeout).FirstOrDefault() as
                                            InventoryFolder;
                                    break;
                                default:
                                    inventoryFolder =
                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                            folder, corradeConfiguration.ServicesTimeout).FirstOrDefault() as
                                            InventoryFolder;
                                    break;
                            }
                            if (inventoryFolder == null)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.FOLDER_NOT_FOUND);
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
                        Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message)).ToLowerInvariant()))
                    {
                        case Enumerations.Action.ACCEPT:
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
                        case Enumerations.Action.DECLINE:
                            lock (InventoryOffersLock)
                            {
                                offer.Key.Accept = false;
                                offer.Value.Set();
                            }
                            break;
                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
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