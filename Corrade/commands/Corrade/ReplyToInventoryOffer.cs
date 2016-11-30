///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using Corrade.Constants;
using Corrade.Structures;
using CorradeConfigurationSharp;
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

                        InventoryFolder inventoryFolder = null;
                        InventoryOffer inventoryOffer = null;
                        var sessionUUID = UUID.Zero;
                        var action =
                            Reflection.GetEnumValueFromName<Enumerations.Action>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                        corradeCommandParameters.Message)).ToLowerInvariant());
                        switch (action)
                        {
                            case Enumerations.Action.ACCEPT:
                                var folder = wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FOLDER)),
                                        corradeCommandParameters.Message));
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
                                                        inventoryFolder =
                                                            Client.Inventory.Store[folderUUID] as InventoryFolder;
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
                                                    Client.Inventory.FindFolderForType(inventoryOffer.Args.AssetType)]
                                                    .Data as InventoryFolder;
                                        }
                                        break;
                                }
                                goto case Enumerations.Action.IGNORE;
                            case Enumerations.Action.DECLINE:
                            case Enumerations.Action.IGNORE:
                                if (
                                    !UUID.TryParse(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SESSION)),
                                                corradeCommandParameters.Message)),
                                        out sessionUUID))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.NO_SESSION_SPECIFIED);
                                }
                                lock (InventoryOffersLock)
                                {
                                    if (!InventoryOffers.TryGetValue(sessionUUID, out inventoryOffer))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.INVENTORY_OFFER_NOT_FOUND);
                                }
                                break;
                        }

                        switch (action)
                        {
                            case Enumerations.Action.ACCEPT:
                                lock (InventoryOffersLock)
                                {
                                    if (!inventoryFolder.UUID.Equals(UUID.Zero))
                                    {
                                        inventoryOffer.Args.FolderID = inventoryFolder.UUID;
                                    }
                                    var name = wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                                            corradeCommandParameters.Message));
                                    if (!string.IsNullOrEmpty(name))
                                        inventoryOffer.Name = name;
                                    inventoryOffer.Args.Accept = true;
                                    inventoryOffer.Event.Set();
                                }
                                break;
                            case Enumerations.Action.DECLINE:
                                lock (InventoryOffersLock)
                                {
                                    inventoryOffer.Args.Accept = false;
                                    inventoryOffer.Event.Set();
                                }
                                break;
                            case Enumerations.Action.PURGE:
                                lock (InventoryOffersLock)
                                {
                                    InventoryOffers.Clear();
                                }
                                break;
                            case Enumerations.Action.IGNORE:
                                lock (InventoryOffersLock)
                                {
                                    InventoryOffers.Remove(sessionUUID);
                                }
                                break;
                            default:
                                throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                        }
                    };
        }
    }
}