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
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    var attachments =
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ATTACHMENTS)),
                                corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(attachments))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.EMPTY_ATTACHMENTS);
                    }

                    var type = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(type))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_TYPE_PROVIDED);
                    }
                    var detachType = Reflection.GetEnumValueFromName<Enumerations.Type>(type.ToLowerInvariant());

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
                            lock (Locks.ClientInstanceSelfLock)
                            {
                                Client.Self.SignaledAnimations.Copy()
                                    .Keys.AsParallel()
                                    .Where(o => !wasOpenMetaverse.Helpers.LindenAnimations.Contains(o))
                                    .ForAll(o => { Client.Self.AnimationStop(o, true); });
                            }
                            break;
                    }
                    var attached =
                        new HashSet<KeyValuePair<Primitive, AttachmentPoint>>(Inventory.GetAttachments(Client,
                            corradeConfiguration.DataTimeout));
                    CSV.ToEnumerable(
                        attachments).AsParallel().Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                        {
                            InventoryItem inventoryItem = null;
                            switch (detachType)
                            {
                                case Enumerations.Type.SLOT:
                                    AttachmentPoint attachmentPoint;
                                    if (attachmentPoints.TryGetValue(o, out attachmentPoint))
                                    {
                                        var attachment =
                                            attached.AsParallel().FirstOrDefault(p => p.Value.Equals(attachmentPoint));
                                        if (!attachment.Equals(default(KeyValuePair<Primitive, AttachmentPoint>)))
                                        {
                                            lock (Locks.ClientInstanceInventoryLock)
                                            {
                                                if (Client.Inventory.Store.Contains(attachment.Key.Properties.ItemID))
                                                {
                                                    inventoryItem =
                                                        Client.Inventory.Store[attachment.Key.Properties.ItemID] as
                                                            InventoryItem;
                                                }
                                            }
                                        }
                                    }
                                    break;
                                case Enumerations.Type.PATH:
                                    inventoryItem =
                                        Inventory.FindInventory<InventoryItem>(Client, o,
                                            CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                            corradeConfiguration.ServicesTimeout);
                                    break;
                                case Enumerations.Type.UUID:
                                    UUID itemUUID;
                                    if (UUID.TryParse(o, out itemUUID))
                                    {
                                        lock (Locks.ClientInstanceInventoryLock)
                                        {
                                            if (Client.Inventory.Store.Contains(itemUUID))
                                            {
                                                inventoryItem = Client.Inventory.Store[itemUUID] as InventoryItem;
                                            }
                                        }
                                    }
                                    break;
                            }
                            if (inventoryItem == null)
                                return;

                            if (inventoryItem is InventoryObject || inventoryItem is InventoryAttachment)
                            {
                                var attachment = attached
                                    .ToArray()
                                    .AsParallel()
                                    .FirstOrDefault(
                                        p =>
                                            p.Key.Properties.ItemID.Equals(
                                                inventoryItem.UUID));
                                // Item not attached.
                                if (attachment.Equals(default(KeyValuePair<Primitive, AttachmentPoint>)))
                                    return;
                                var slot = attachment.Value.ToString();
                                CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                    () => SendNotification(
                                        Configuration.Notifications.OutfitChanged,
                                        new OutfitEventArgs
                                        {
                                            Action = Enumerations.Action.DETACH,
                                            Name = inventoryItem.Name,
                                            Description = inventoryItem.Description,
                                            Item = inventoryItem.UUID,
                                            Asset = inventoryItem.AssetUUID,
                                            Entity = inventoryItem.AssetType,
                                            Creator = inventoryItem.CreatorID,
                                            Permissions =
                                                Inventory.wasPermissionsToString(
                                                    inventoryItem.Permissions),
                                            Inventory = inventoryItem.InventoryType,
                                            Slot = slot
                                        }),
                                    corradeConfiguration.MaximumNotificationThreads);
                                Inventory.Detach(Client, CurrentOutfitFolder, inventoryItem,
                                    corradeConfiguration.ServicesTimeout);
                            }
                        });
                    RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                };
        }
    }
}