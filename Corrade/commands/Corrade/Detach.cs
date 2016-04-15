///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
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

                    string type = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(type))
                    {
                        throw new ScriptException(ScriptError.NO_TYPE_PROVIDED);
                    }
                    Type detachType = Reflection.GetEnumValueFromName<Type>(type.ToLowerInvariant());

                    // build a look-up table for the attachment points
                    Dictionary<string, AttachmentPoint> attachmentPoints =
                        new Dictionary<string, AttachmentPoint>(typeof (AttachmentPoint).GetFields(BindingFlags.Public |
                                                                                                   BindingFlags.Static)
                            .AsParallel().ToDictionary(o => o.Name, o => (AttachmentPoint) o.GetValue(null)));

                    // stop non default animations if requested
                    bool deanimate;
                    switch (bool.TryParse(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DEANIMATE)),
                            corradeCommandParameters.Message)), out deanimate) && deanimate)
                    {
                        case true:
                            // stop all non-built-in animations
                            Client.Self.SignaledAnimations.Copy()
                                .Keys.AsParallel()
                                .Where(o => !Helpers.LindenAnimations.Contains(o))
                                .ForAll(o => { Client.Self.AnimationStop(o, true); });
                            break;
                    }

                    CSV.ToEnumerable(
                        attachments).ToArray().AsParallel().Where(o => !string.IsNullOrEmpty(o)).ForAll(o =>
                        {
                            InventoryBase inventoryBaseItem = null;
                            switch (detachType)
                            {
                                case Type.SLOT:
                                    AttachmentPoint attachmentPoint;
                                    if (attachmentPoints.TryGetValue(o, out attachmentPoint))
                                    {
                                        KeyValuePair<Primitive, AttachmentPoint> attachment =
                                            Inventory.GetAttachments(Client, corradeConfiguration.DataTimeout)
                                                .AsParallel().FirstOrDefault(p => p.Value.Equals(attachmentPoint));
                                        if (!attachment.Equals(default(KeyValuePair<Primitive, AttachmentPoint>)))
                                        {
                                            inventoryBaseItem =
                                                Inventory.FindInventory<InventoryBase>(Client,
                                                    Client.Inventory.Store.RootNode,
                                                    attachment.Key.Properties.ItemID
                                                    )
                                                    .AsParallel().FirstOrDefault(
                                                        p =>
                                                            p is InventoryItem &&
                                                            ((InventoryItem) p).AssetType.Equals(AssetType.Object));
                                            if (inventoryBaseItem == null)
                                                return;
                                        }
                                    }
                                    break;
                                case Type.NAME:
                                    // attempt regex and then fall back to string
                                    try
                                    {
                                        inventoryBaseItem =
                                            Inventory.FindInventory<InventoryBase>(Client,
                                                Client.Inventory.Store.RootNode,
                                                new Regex(o, RegexOptions.Compiled | RegexOptions.IgnoreCase))
                                                .FirstOrDefault();
                                    }
                                    catch (Exception)
                                    {
                                        // not a regex so we do not care
                                        inventoryBaseItem =
                                            Inventory.FindInventory<InventoryBase>(Client,
                                                Client.Inventory.Store.RootNode,
                                                o)
                                                .FirstOrDefault();
                                    }
                                    if (inventoryBaseItem == null)
                                        return;
                                    break;
                                case Type.UUID:
                                    UUID itemUUID;
                                    if (UUID.TryParse(o, out itemUUID))
                                    {
                                        inventoryBaseItem =
                                            Inventory.FindInventory<InventoryBase>(Client,
                                                Client.Inventory.Store.RootNode,
                                                itemUUID
                                                ).FirstOrDefault();
                                        if (inventoryBaseItem == null)
                                            return;
                                    }
                                    break;
                            }
                            InventoryItem inventoryItem = inventoryBaseItem as InventoryItem;
                            if (inventoryItem == null)
                                return;

                            if (inventoryItem is InventoryObject || inventoryItem is InventoryAttachment)
                            {
                                CorradeThreadPool[CorradeThreadType.NOTIFICATION].Spawn(
                                    () => SendNotification(
                                        Configuration.Notifications.OutfitChanged,
                                        new OutfitEventArgs
                                        {
                                            Action = Action.DETACH,
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
                                            Slot = Inventory.GetAttachments(
                                                Client,
                                                corradeConfiguration.DataTimeout)
                                                .AsParallel()
                                                .Where(
                                                    p =>
                                                        p.Key.Properties.ItemID.Equals(
                                                            inventoryItem.UUID))
                                                .Select(p => p.Value.ToString())
                                                .FirstOrDefault()
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