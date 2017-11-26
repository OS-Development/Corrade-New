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
                            throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);

                        InventoryFolder inventoryFolder = null;
                        InventoryOffer inventoryOffer = null;
                        var sessionUUID = UUID.Zero;
                        var action =
                            Reflection.GetEnumValueFromName<Enumerations.Action>(
                                wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                        corradeCommandParameters.Message)));
                        switch (action)
                        {
                            case Enumerations.Action.ACCEPT:
                                var folder = wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FOLDER)),
                                        corradeCommandParameters.Message));
                                if (!string.IsNullOrEmpty(folder))
                                {
                                    UUID folderUUID;
                                    switch (UUID.TryParse(folder, out folderUUID))
                                    {
                                        case true:
                                            Locks.ClientInstanceInventoryLock.EnterReadLock();
                                            if (Client.Inventory.Store.Contains(folderUUID))
                                                inventoryFolder =
                                                    Client.Inventory.Store[folderUUID] as InventoryFolder;
                                            Locks.ClientInstanceInventoryLock.ExitReadLock();
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
                                        throw new Command.ScriptException(Enumerations.ScriptError.FOLDER_NOT_FOUND);
                                }
                                goto case Enumerations.Action.DECLINE;
                            case Enumerations.Action.IGNORE:
                            case Enumerations.Action.DECLINE:
                                if (
                                    !UUID.TryParse(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SESSION)),
                                                corradeCommandParameters.Message)),
                                        out sessionUUID))
                                    throw new Command.ScriptException(Enumerations.ScriptError.NO_SESSION_SPECIFIED);
                                lock (InventoryOffersLock)
                                {
                                    if (!InventoryOffers.TryGetValue(sessionUUID, out inventoryOffer))
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.INVENTORY_OFFER_NOT_FOUND);

                                    // If the asset folder exists, then set the accept folder to the asset folder.
                                    var folderUUID = Client.Inventory.FindFolderForType(inventoryOffer.Args.AssetType);
                                    if (Client.Inventory.Store.Contains(folderUUID))
                                        inventoryFolder =
                                            Client.Inventory.Store[folderUUID] as InventoryFolder;
                                }
                                break;
                        }

                        switch (action)
                        {
                            case Enumerations.Action.ACCEPT:
                                lock (InventoryOffersLock)
                                {
                                    // Set the folder if specified.
                                    if (inventoryFolder != null && !inventoryFolder.UUID.Equals(UUID.Zero))
                                        inventoryOffer.Args.FolderID = inventoryFolder.UUID;
                                    var name = wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                                            corradeCommandParameters.Message));
                                    if (!string.IsNullOrEmpty(name))
                                        inventoryOffer.Name = name;
                                    inventoryOffer.Args.Accept = true;
                                    inventoryOffer.Event.Set();
                                }

                                // Send the reply.
                                switch (inventoryOffer.Args.Offer.Dialog)
                                {
                                    case InstantMessageDialog.InventoryOffered:
                                        Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                        Client.Self.InstantMessage(Client.Self.Name,
                                            inventoryOffer.Args.Offer.FromAgentID,
                                            string.Empty, inventoryOffer.Args.Offer.IMSessionID,
                                            InstantMessageDialog.InventoryAccepted,
                                            InstantMessageOnline.Offline,
                                            Client.Self.SimPosition,
                                            Client.Network.CurrentSim.RegionID,
                                            inventoryOffer.Args.FolderID.GetBytes()
                                        );
                                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                        break;

                                    case InstantMessageDialog.TaskInventoryOffered:
                                        Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                        Client.Self.InstantMessage(Client.Self.Name,
                                            inventoryOffer.Args.Offer.FromAgentID,
                                            string.Empty, inventoryOffer.Args.Offer.IMSessionID,
                                            InstantMessageDialog.TaskInventoryAccepted,
                                            InstantMessageOnline.Offline,
                                            Client.Self.SimPosition,
                                            Client.Network.CurrentSim.RegionID,
                                            inventoryOffer.Args.FolderID.GetBytes()
                                        );
                                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                        break;
                                }
                                break;

                            case Enumerations.Action.DECLINE:
                                lock (InventoryOffersLock)
                                {
                                    inventoryOffer.Args.Accept = false;
                                    inventoryOffer.Event.Set();
                                }

                                // Send the reply.
                                switch (inventoryOffer.Args.Offer.Dialog)
                                {
                                    case InstantMessageDialog.InventoryOffered:
                                        Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                        Client.Self.InstantMessage(Client.Self.Name,
                                            inventoryOffer.Args.Offer.FromAgentID,
                                            string.Empty, inventoryOffer.Args.Offer.IMSessionID,
                                            InstantMessageDialog.InventoryDeclined,
                                            InstantMessageOnline.Offline,
                                            Client.Self.SimPosition,
                                            Client.Network.CurrentSim.RegionID,
                                            Utils.EmptyBytes
                                        );
                                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                        break;

                                    case InstantMessageDialog.TaskInventoryOffered:
                                        Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                        Client.Self.InstantMessage(Client.Self.Name,
                                            inventoryOffer.Args.Offer.FromAgentID,
                                            string.Empty, inventoryOffer.Args.Offer.IMSessionID,
                                            InstantMessageDialog.TaskInventoryDeclined,
                                            InstantMessageOnline.Offline,
                                            Client.Self.SimPosition,
                                            Client.Network.CurrentSim.RegionID,
                                            Utils.EmptyBytes
                                        );
                                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                        break;
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