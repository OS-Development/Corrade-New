///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using Corrade.Constants;
using Corrade.Events;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> detach =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    var attachments =
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ATTACHMENTS)),
                                corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(attachments))
                        throw new Command.ScriptException(Enumerations.ScriptError.EMPTY_ATTACHMENTS);

                    var type = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(type))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_TYPE_PROVIDED);
                    var detachType = Reflection.GetEnumValueFromName<Enumerations.Type>(type);

                    // build a look-up table for the attachment points
                    var attachmentPoints =
                        new Dictionary<string, AttachmentPoint>(typeof(AttachmentPoint).GetFields(BindingFlags.Public |
                                                                                                  BindingFlags.Static)
                            .AsParallel().ToDictionary(o => o.Name, o => (AttachmentPoint) o.GetValue(null)));

                    // stop non default animations if requested
                    bool deanimate;
                    switch (bool.TryParse(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DEANIMATE)),
                                    corradeCommandParameters.Message)), out deanimate) && deanimate)
                    {
                        case true:
                            // stop all non-built-in animations
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.SignaledAnimations.Copy()
                                .Keys.AsParallel()
                                .Where(o => !wasOpenMetaverse.Helpers.LindenAnimations.Contains(o))
                                .ForAll(o => { Client.Self.AnimationStop(o, true); });
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;
                    }

                    var currentAttachments = Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout);
                    // get current attachments.
                    var attachedPrimitives = currentAttachments
                        .ToDictionary(o => o.Key.Properties.ItemID, o => o.Value.ToString());

                    var attachedSlots = currentAttachments
                        .ToLookup(o => o.Value, o => o.Key);

                    var LockObject = new object();
                    CSV.ToEnumerable(attachments).AsParallel().Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                    {
                        var inventoryItems = new List<InventoryItem>();
                        switch (detachType)
                        {
                            case Enumerations.Type.SLOT:
                                AttachmentPoint attachmentPoint;
                                if (attachmentPoints.TryGetValue(o, out attachmentPoint))
                                    if (attachedSlots.Contains(attachmentPoint))
                                        attachedSlots[attachmentPoint]
                                            .AsParallel()
                                            .Select(r => r.Properties.ItemID)
                                            .Where(r => Client.Inventory.Store.Contains(r))
                                            .ForAll(r =>
                                            {
                                                Locks.ClientInstanceInventoryLock.EnterReadLock();
                                                var inventoryItem = Client.Inventory.Store[r] as InventoryItem;
                                                Locks.ClientInstanceInventoryLock.ExitReadLock();

                                                // Add the inventory item.
                                                lock (LockObject)
                                                {
                                                    inventoryItems.Add(inventoryItem);
                                                }
                                            });
                                break;

                            case Enumerations.Type.PATH:
                                var item =
                                    Inventory.FindInventory<InventoryItem>(Client, o,
                                        CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                        corradeConfiguration.ServicesTimeout);

                                if (item == null)
                                    break;

                                lock (LockObject)
                                {
                                    inventoryItems.Add(item);
                                }
                                break;

                            case Enumerations.Type.UUID:
                                UUID itemUUID;
                                if (UUID.TryParse(o, out itemUUID))
                                {
                                    Locks.ClientInstanceInventoryLock.EnterReadLock();
                                    if (Client.Inventory.Store.Contains(itemUUID))
                                    {
                                        var inventoryItem = Client.Inventory.Store[itemUUID] as InventoryItem;
                                        if (inventoryItem == null)
                                        {
                                            Locks.ClientInstanceInventoryLock.ExitReadLock();
                                            break;
                                        }
                                        lock (LockObject)
                                        {
                                            inventoryItems.Add(inventoryItem);
                                        }
                                    }
                                    Locks.ClientInstanceInventoryLock.ExitReadLock();
                                }
                                break;
                        }
                        if (!inventoryItems.Any())
                            return;

                        inventoryItems.AsParallel().ForAll(p =>
                        {
                            if (p is InventoryObject || p is InventoryAttachment)
                            {
                                var slot = string.Empty;
                                if (!attachedPrimitives.TryGetValue(p.UUID, out slot))
                                    return;

                                CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                    () => SendNotification(
                                        Configuration.Notifications.OutfitChanged,
                                        new OutfitEventArgs
                                        {
                                            Action = Enumerations.Action.DETACH,
                                            Name = p.Name,
                                            Description = p.Description,
                                            Item = p.UUID,
                                            Asset = p.AssetUUID,
                                            Entity = p.AssetType,
                                            Creator = p.CreatorID,
                                            Permissions =
                                                Inventory.wasPermissionsToString(
                                                    p.Permissions),
                                            Inventory = p.InventoryType,
                                            Slot = slot
                                        }),
                                    corradeConfiguration.MaximumNotificationThreads);

                                Inventory.Detach(Client, CurrentOutfitFolder, p,
                                    corradeConfiguration.ServicesTimeout);
                            }
                        });
                    });
                    RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                };
        }
    }
}