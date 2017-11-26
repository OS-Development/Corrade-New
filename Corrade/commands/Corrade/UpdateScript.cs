///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using Corrade.Constants;
using CorradeConfigurationSharp;
using OpenMetaverse;
using OpenMetaverse.Assets;
using OpenMetaverse.Packets;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> updatescript
                =
                (corradeCommandParameters, result) =>
                {
                    var type = Reflection.GetEnumValueFromName<Enumerations.Type>(
                        wasInput(KeyValue.Get(wasOutput(
                                Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                            corradeCommandParameters.Message)));
                    switch (type)
                    {
                        case Enumerations.Type.TASK:
                            if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Interact))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                            break;

                        case Enumerations.Type.AGENT:
                            if (!HasCorradePermission(corradeCommandParameters.Group.UUID,
                                (int) Configuration.Permissions.Inventory))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_UPDATE_TYPE);
                    }

                    var mono = true;
                    if (!bool.TryParse(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MONO)),
                            corradeCommandParameters.Message)), out mono))
                        mono = true;

                    var run = true;
                    if (!bool.TryParse(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RUN)),
                            corradeCommandParameters.Message)), out run))
                        run = true;

                    var csv = new List<string>();
                    InventoryItem inventoryItem = null;
                    var item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    var itemUUID = UUID.Zero;
                    var UpdateScriptEvent = new ManualResetEventSlim(false);
                    var succeeded = false;
                    var target = wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                            corradeCommandParameters.Message));
                    Primitive primitive = null;
                    switch (type)
                    {
                        case Enumerations.Type.TASK:
                            float range;
                            if (
                                !float.TryParse(
                                    wasInput(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                        corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                    out range))
                                range = corradeConfiguration.Range;
                            UUID targetUUID;
                            if (!UUID.TryParse(target, out targetUUID))
                            {
                                if (string.IsNullOrEmpty(target))
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                                targetUUID = UUID.Zero;
                            }
                            if (string.IsNullOrEmpty(item))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                            switch (UUID.TryParse(item, out itemUUID))
                            {
                                case true:
                                    if (
                                        !Services.FindPrimitive(Client,
                                            itemUUID,
                                            range,
                                            ref primitive,
                                            corradeConfiguration.DataTimeout))
                                        throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                                    break;

                                default:
                                    if (
                                        !Services.FindPrimitive(Client,
                                            item,
                                            range,
                                            ref primitive,
                                            corradeConfiguration.DataTimeout))
                                        throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                                    break;
                            }

                            var inventory = new List<InventoryBase>();
                            Locks.ClientInstanceInventoryLock.EnterReadLock();
                            inventory.AddRange(
                                Client.Inventory.GetTaskInventory(primitive.ID, primitive.LocalID,
                                    (int) corradeConfiguration.ServicesTimeout));
                            Locks.ClientInstanceInventoryLock.ExitReadLock();
                            inventoryItem = !targetUUID.Equals(UUID.Zero)
                                ? inventory.AsParallel().FirstOrDefault(o => o.UUID.Equals(targetUUID)) as InventoryItem
                                : inventory.AsParallel().FirstOrDefault(o => o.Name.Equals(target)) as InventoryItem;

                            // If task inventory item does not exist create it.
                            if (inventoryItem == null ||
                                !inventoryItem.AssetType.Equals(AssetType.LSLText))
                            {
                                var create = false;
                                if (!bool.TryParse(wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.CREATE)),
                                            corradeCommandParameters.Message)), out create) || !create)
                                    throw new Command.ScriptException(Enumerations.ScriptError
                                        .INVENTORY_ITEM_NOT_FOUND);

                                var permissions = Permissions.NoPermissions;
                                Inventory.wasStringToPermissions(wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PERMISSIONS)),
                                        corradeCommandParameters.Message)), out permissions);

                                Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                var CreateScriptEvent = new ManualResetEventSlim(false);
                                Client.Inventory.RequestCreateItem(
                                    Client.Inventory.FindFolderForType(AssetType.LSLText),
                                    target,
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                            corradeCommandParameters.Message)),
                                    AssetType.LSLText,
                                    UUID.Random(),
                                    InventoryType.LSL,
                                    permissions.Equals(Permissions.NoPermissions)
                                        ? PermissionMask.Transfer
                                        : permissions.NextOwnerMask,
                                    delegate(bool completed, InventoryItem createdItem)
                                    {
                                        inventoryItem = createdItem;
                                        succeeded = completed;
                                        CreateScriptEvent.Set();
                                    });
                                if (!CreateScriptEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                                {
                                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                    throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_CREATING_ITEM);
                                }
                                Locks.ClientInstanceInventoryLock.ExitWriteLock();

                                if (!succeeded)
                                    throw new Command.ScriptException(Enumerations.ScriptError.ASSET_UPLOAD_FAILED);

                                // Copy the item to the task inventory.
                                Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                Client.Inventory.CopyScriptToTask(primitive.LocalID, inventoryItem, run);
                                Client.Inventory.RemoveItem(inventoryItem.UUID);
                                Locks.ClientInstanceInventoryLock.ExitWriteLock();
                            }
                            break;

                        case Enumerations.Type.AGENT:
                            // If an item was specified then update instead of creating a new item for certain asset types.
                            if (!string.IsNullOrEmpty(item))
                            {
                                switch (UUID.TryParse(item, out itemUUID))
                                {
                                    case true:
                                        Locks.ClientInstanceInventoryLock.EnterReadLock();
                                        if (Client.Inventory.Store.Contains(itemUUID))
                                            inventoryItem = Client.Inventory.Store[itemUUID] as InventoryItem;
                                        Locks.ClientInstanceInventoryLock.ExitReadLock();
                                        break;

                                    default:
                                        inventoryItem = Inventory.FindInventory<InventoryBase>(Client, item,
                                            CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                            corradeConfiguration.ServicesTimeout) as InventoryItem;
                                        break;
                                }

                                if (inventoryItem == null || !inventoryItem.AssetType.Equals(AssetType.LSLText))
                                {
                                    var create = false;
                                    if (!bool.TryParse(wasInput(KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.CREATE)),
                                            corradeCommandParameters.Message)), out create) || !create)
                                        throw new Command.ScriptException(Enumerations.ScriptError
                                            .INVENTORY_ITEM_NOT_FOUND);

                                    var permissions = Permissions.NoPermissions;
                                    Inventory.wasStringToPermissions(wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PERMISSIONS)),
                                            corradeCommandParameters.Message)), out permissions);

                                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                                    var CreateScriptEvent = new ManualResetEventSlim(false);
                                    Client.Inventory.RequestCreateItem(
                                        Client.Inventory.FindFolderForType(AssetType.LSLText),
                                        target,
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(
                                                    Command.ScriptKeys.DESCRIPTION)),
                                                corradeCommandParameters.Message)),
                                        AssetType.LSLText,
                                        UUID.Random(), InventoryType.LSL,
                                        permissions.Equals(Permissions.NoPermissions)
                                            ? PermissionMask.Transfer
                                            : permissions.NextOwnerMask,
                                        delegate(bool completed, InventoryItem createdItem)
                                        {
                                            inventoryItem = createdItem;
                                            succeeded = completed;
                                            CreateScriptEvent.Set();
                                        });
                                    if (!CreateScriptEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                                    {
                                        Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.TIMEOUT_CREATING_ITEM);
                                    }
                                    Locks.ClientInstanceInventoryLock.ExitWriteLock();

                                    if (!succeeded)
                                        throw new Command.ScriptException(Enumerations.ScriptError.ASSET_UPLOAD_FAILED);
                                }
                            }
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_UPDATE_TYPE);
                    }

                    var temporary = false;
                    if (!bool.TryParse(wasInput(
                        KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TEMPORARY)),
                            corradeCommandParameters.Message)), out temporary))
                        temporary = false;

                    AssetScriptText script = null;
                    switch (Reflection.GetEnumValueFromName<Enumerations.Entity>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message))))
                    {
                        case Enumerations.Entity.FILE:
                            if (
                                !HasCorradePermission(corradeCommandParameters.Group.UUID,
                                    (int) Configuration.Permissions.System))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                            var path =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PATH)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(path))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_PATH_PROVIDED);
                            // Read from file.
                            var data = string.Empty;
                            try
                            {
                                using (
                                    var fileStream = new FileStream(path, FileMode.Open, FileAccess.Read,
                                        FileShare.Read,
                                        16384, true))
                                {
                                    using (var streamReader = new StreamReader(fileStream, Encoding.UTF8))
                                    {
                                        data = streamReader.ReadToEnd();
                                    }
                                }
                            }
                            catch (Exception ex)
                            {
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA), ex.Message);
                                throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_READ_FILE);
                            }
                            script = new AssetScriptText(inventoryItem.AssetUUID, null)
                            {
                                Source = data,
                                Temporary = temporary
                            };
                            break;

                        case Enumerations.Entity.TEXT:
                            var text =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                        corradeCommandParameters.Message));
                            if (string.IsNullOrEmpty(text))
                                data = wasOpenMetaverse.Constants.ASSETS.NOTECARD.NEWLINE;

                            script = new AssetScriptText(inventoryItem.AssetUUID, null)
                            {
                                Source = text,
                                Temporary = temporary
                            };
                            break;

                        case Enumerations.Entity.ASSET:
                            byte[] asset = null;
                            try
                            {
                                asset = Convert.FromBase64String(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                            corradeCommandParameters.Message)));
                            }
                            catch (Exception)
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);
                            }

                            if (asset == null || asset.Length.Equals(0))
                                throw new Command.ScriptException(Enumerations.ScriptError.EMPTY_ASSET_DATA);

                            var assetScriptText = new AssetScriptText
                            {
                                AssetData = asset
                            };

                            if (!assetScriptText.Decode())
                                throw new Command.ScriptException(Enumerations.ScriptError.INVALID_ASSET_DATA);

                            script = new AssetScriptText(inventoryItem.AssetUUID, null)
                            {
                                Source = assetScriptText.Source,
                                Temporary = temporary
                            };
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                    }

                    script.Encode();

                    switch (type)
                    {
                        case Enumerations.Type.TASK:
                            var reset = true;
                            if (!bool.TryParse(wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RESET)),
                                    corradeCommandParameters.Message)), out reset))
                                reset = true;

                            // Update the script inside the task inventory.
                            Locks.ClientInstanceInventoryLock.EnterWriteLock();
                            Client.Inventory.RequestUpdateScriptTask(script.AssetData, inventoryItem.UUID, primitive.ID,
                                mono, run,
                                delegate(bool completed, string status, bool compiled, List<string> messages,
                                    UUID itemID, UUID assetID)
                                {
                                    // Add the compiler output to the return.
                                    if (!compiled && messages.Any())
                                        csv.AddRange(new[]
                                        {
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ERROR)),
                                            CSV.FromEnumerable(messages)
                                        });
                                    inventoryItem.UUID = itemID;
                                    inventoryItem.AssetUUID = assetID;
                                    succeeded = completed;
                                    UpdateScriptEvent.Set();
                                });
                            if (!UpdateScriptEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                            {
                                Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ASSET);
                            }
                            Locks.ClientInstanceInventoryLock.ExitWriteLock();

                            if (!succeeded)
                                throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_UPLOAD_ITEM);

                            var simulator = Client.Network.Simulators.FirstOrDefault(
                                o => o.Handle.Equals(primitive.RegionHandle));
                            if (simulator == null || simulator.Equals(default(Simulator)))
                                throw new Command.ScriptException(Enumerations.ScriptError.REGION_NOT_FOUND);

                            // Reset script if requested.
                            if (reset)
                            {
                                Locks.ClientInstanceNetworkLock.EnterReadLock();
                                Client.Network.SendPacket(new ScriptResetPacket
                                {
                                    Type = PacketType.ScriptReset,
                                    AgentData = new ScriptResetPacket.AgentDataBlock
                                    {
                                        AgentID = Client.Self.AgentID,
                                        SessionID = Client.Self.SessionID
                                    },
                                    Script = new ScriptResetPacket.ScriptBlock
                                    {
                                        ItemID = inventoryItem.UUID,
                                        ObjectID = primitive.ID
                                    }
                                }, simulator);
                                Locks.ClientInstanceNetworkLock.ExitReadLock();
                            }
                            break;

                        case Enumerations.Type.AGENT:
                            Locks.ClientInstanceInventoryLock.EnterWriteLock();
                            Client.Inventory.RequestUpdateScriptAgentInventory(script.AssetData, inventoryItem.UUID,
                                mono,
                                delegate(bool completed, string status, bool compiled, List<string> messages,
                                    UUID itemID, UUID assetID)
                                {
                                    // Add the compiler output to the return.
                                    if (!compiled && messages.Any())
                                        csv.AddRange(new[]
                                        {
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ERROR)),
                                            CSV.FromEnumerable(messages)
                                        });
                                    inventoryItem.UUID = itemID;
                                    inventoryItem.AssetUUID = assetID;
                                    succeeded = completed;
                                    UpdateScriptEvent.Set();
                                });
                            if (!UpdateScriptEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                            {
                                Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ASSET);
                            }
                            Locks.ClientInstanceInventoryLock.ExitWriteLock();

                            if (!succeeded)
                                throw new Command.ScriptException(Enumerations.ScriptError.UNABLE_TO_UPLOAD_ITEM);
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_UPDATE_TYPE);
                    }

                    // Add the item and assetUUID ot the output.
                    csv.AddRange(new[]
                    {
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                        inventoryItem.UUID.ToString(),
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ASSET)),
                        inventoryItem.AssetUUID.ToString()
                    });
                    // Return the item and asset UUID.
                    result.Add(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                        CSV.FromEnumerable(csv));
                };
        }
    }
}