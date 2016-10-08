///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using Corrade.Constants;
using Corrade.Structures;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>>
                replytoinventoryoffer =
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
                        InventoryOffer offer;
                        lock (InventoryOffersLock)
                        {
                            offer =
                                InventoryOffers.AsParallel()
                                    .FirstOrDefault(o => o.Args.Offer.IMSessionID.Equals(session));
                        }
                        if (offer == null)
                        {
                            throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_OFFER_NOT_FOUND);
                        }
                        var folder = wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FOLDER)),
                                corradeCommandParameters.Message));
                        InventoryFolder inventoryFolder = null;
                        switch (!string.IsNullOrEmpty(folder))
                        {
                            case true:
                                UUID folderUUID;
                                switch (UUID.TryParse(folder, out folderUUID))
                                {
                                    case true:
                                        lock (Locks.ClientInstanceInventoryLock)
                                        {
                                            if (Client.Inventory.Store.Contains(folderUUID))
                                            {
                                                inventoryFolder = Client.Inventory.Store[folderUUID] as InventoryFolder;
                                            }
                                        }
                                        break;
                                    default:
                                        inventoryFolder =
                                            Inventory.FindInventory<InventoryFolder>(Client, folder,
                                                CORRADE_CONSTANTS.PATH_SEPARATOR,
                                                CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                                corradeConfiguration.ServicesTimeout);
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
                                        Client.Inventory.Store.Items[
                                            Client.Inventory.FindFolderForType(offer.Args.AssetType)]
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
                                        offer.Args.FolderID = inventoryFolder.UUID;
                                    }
                                    var name = wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                                            corradeCommandParameters.Message));
                                    if (!string.IsNullOrEmpty(name))
                                        offer.Name = name;
                                    offer.Args.Accept = true;
                                    offer.Event.Set();
                                }
                                break;
                            case Enumerations.Action.DECLINE:
                                lock (InventoryOffersLock)
                                {
                                    offer.Args.Accept = false;
                                    offer.Event.Set();
                                }
                                break;
                            default:
                                throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                        }
                    };
        }
    }
}