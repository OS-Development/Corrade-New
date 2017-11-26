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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> attach =
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
                    bool replace;
                    if (
                        !bool.TryParse(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.REPLACE)),
                                    corradeCommandParameters.Message)),
                            out replace))
                        replace = true;
                    var items = CSV.ToKeyValue(attachments)
                        .ToLookup(o => wasInput(o.Key), o => wasInput(o.Value));
                    // if this is SecondLife, check that the additional attachments would not exceed the maximum attachment limit
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client))
                        switch (replace)
                        {
                            case true:
                                if (
                                    Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                                        .Count() + items.Count -
                                    typeof(AttachmentPoint).GetFields(
                                            BindingFlags.Public | BindingFlags.Static)
                                        .AsParallel().Count(p => !items.Contains(p.Name)) >
                                    wasOpenMetaverse.Constants.AVATARS.MAXIMUM_NUMBER_OF_ATTACHMENTS)
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.ATTACHMENTS_WOULD_EXCEED_MAXIMUM_ATTACHMENT_LIMIT);
                                break;

                            default:
                                if (items.Count +
                                    Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                                        .Count() >
                                    wasOpenMetaverse.Constants.AVATARS.MAXIMUM_NUMBER_OF_ATTACHMENTS)
                                    throw new Command.ScriptException(
                                        Enumerations.ScriptError.ATTACHMENTS_WOULD_EXCEED_MAXIMUM_ATTACHMENT_LIMIT);
                                break;
                        }

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

                    // build a look-up table for the attachment points
                    var attachmentPoints =
                        new Dictionary<string, AttachmentPoint>(typeof(AttachmentPoint).GetFields(BindingFlags.Public |
                                                                                                  BindingFlags.Static)
                            .AsParallel().ToDictionary(o => o.Name, o => (AttachmentPoint) o.GetValue(null)));

                    // get current attachments.
                    var currentAttachments = Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                        .ToDictionary(o => o.Key.Properties.ItemID, o => o.Value.ToString());

                    items.AsParallel()
                        .Where(o => attachmentPoints.ContainsKey(o.Key))
                        .Select(o => new {Point = attachmentPoints[o.Key], Items = items[o.Key]}).ForAll(o =>
                            o.Items.AsParallel().ForAll(p =>
                            {
                                InventoryItem inventoryItem = null;
                                UUID itemUUID;
                                switch (UUID.TryParse(p, out itemUUID))
                                {
                                    case true:
                                        Locks.ClientInstanceInventoryLock.EnterReadLock();
                                        if (Client.Inventory.Store.Contains(itemUUID))
                                            inventoryItem =
                                                Client.Inventory.Store[itemUUID] as InventoryItem;
                                        Locks.ClientInstanceInventoryLock.ExitReadLock();
                                        break;

                                    default:
                                        inventoryItem = Inventory.FindInventory<InventoryItem>(
                                            Client, p, CORRADE_CONSTANTS.PATH_SEPARATOR,
                                            CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                            corradeConfiguration.ServicesTimeout);
                                        break;
                                }

                                if (inventoryItem == null)
                                    return;

                                if (inventoryItem is InventoryObject || inventoryItem is InventoryAttachment)
                                {
                                    Inventory.Attach(Client, CurrentOutfitFolder,
                                        inventoryItem,
                                        o.Point,
                                        replace, corradeConfiguration.ServicesTimeout);

                                    var slot = string.Empty;
                                    if (!currentAttachments.TryGetValue(inventoryItem.UUID, out slot))
                                        slot = AttachmentPoint.Default.ToString();

                                    CorradeThreadPool[Threading.Enumerations.ThreadType.NOTIFICATION].Spawn(
                                        () => SendNotification(
                                            Configuration.Notifications.OutfitChanged,
                                            new OutfitEventArgs
                                            {
                                                Action = Enumerations.Action.ATTACH,
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
                                                Replace = replace,
                                                Slot = slot
                                            }),
                                        corradeConfiguration.MaximumNotificationThreads);
                                }
                            }));
                    RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                };
        }
    }
}