///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using Corrade.Constants;
using CorradeConfigurationSharp;
using OpenMetaverse;
using System;
using System.Text;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;
using Inventory = wasOpenMetaverse.Inventory;
using System.IO;

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
                    var data = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                                    corradeCommandParameters.Message));

                    if (!string.IsNullOrEmpty(data))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_DATA_PROVIDED);

                    bool mono;
                    if (!bool.TryParse(wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MONO)),
                            corradeCommandParameters.Message)), out mono))
                    {
                        mono = true;
                    }

                    var csv = new List<string>();
                    InventoryItem inventoryItem = null;
                    var item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    var itemUUID = UUID.Zero;
                    var UpdateScriptEvent = new ManualResetEvent(false);
                    switch (
                        Reflection.GetEnumValueFromName<Enumerations.Type>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                    corradeCommandParameters.Message))))
                    {
                        case Enumerations.Type.TASK:
                            if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int)Configuration.Permissions.Interact))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                            }
                            float range;
                            if (
                                !float.TryParse(
                                    wasInput(KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                        corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                                    out range))
                            {
                                range = corradeConfiguration.Range;
                            }
                            var entity =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ENTITY)),
                                        corradeCommandParameters.Message));
                            UUID entityUUID;
                            if (!UUID.TryParse(entity, out entityUUID))
                            {
                                if (string.IsNullOrEmpty(entity))
                                {
                                    throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ENTITY);
                                }
                                entityUUID = UUID.Zero;
                            }
                            Primitive primitive = null;
                            if (string.IsNullOrEmpty(item))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                            }
                            switch (UUID.TryParse(item, out itemUUID))
                            {
                                case true:
                                    if (
                                        !Services.FindPrimitive(Client,
                                            itemUUID,
                                            range,
                                            ref primitive,
                                            corradeConfiguration.DataTimeout))
                                    {
                                        throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                                    }
                                    break;

                                default:
                                    if (
                                        !Services.FindPrimitive(Client,
                                            item,
                                            range,
                                            ref primitive,
                                            corradeConfiguration.DataTimeout))
                                    {
                                        throw new Command.ScriptException(Enumerations.ScriptError.PRIMITIVE_NOT_FOUND);
                                    }
                                    break;
                            }
                            var inventory = new List<InventoryBase>();
                            Locks.ClientInstanceInventoryLock.EnterReadLock();
                            inventory.AddRange(
                                    Client.Inventory.GetTaskInventory(primitive.ID, primitive.LocalID,
                                        (int)corradeConfiguration.ServicesTimeout));
                            Locks.ClientInstanceInventoryLock.ExitReadLock();
                            inventoryItem = !entityUUID.Equals(UUID.Zero)
                                ? inventory.AsParallel().FirstOrDefault(o => o.UUID.Equals(entityUUID)) as InventoryItem
                                : inventory.AsParallel().FirstOrDefault(o => o.Name.Equals(entity)) as InventoryItem;
                            // If task inventory item does not exist create it.
                            bool succeeded = false;
                            if (inventoryItem == null ||
                                !inventoryItem.AssetType.Equals(AssetType.LSLText))
                                throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);

                            bool run;
                            if (!bool.TryParse(wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RUN)),
                                    corradeCommandParameters.Message)), out run))
                            {
                                run = true;
                            }

                            // Update the script inside the task inventory.
                            Locks.ClientInstanceInventoryLock.EnterWriteLock();
                            using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(data)))
                            {
                                Client.Inventory.RequestUpdateScriptTask(memoryStream.ToArray(), inventoryItem.UUID, primitive.ID, mono, run,
                                    delegate (bool completed, string status, bool compiled, List<string> messages,
                                        UUID itemID, UUID assetID)
                                    {
                                        // Add the compiler output to the return.
                                        if (!compiled)
                                            csv.AddRange(new[] {
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ERROR)), CSV.FromEnumerable(messages),
                                            });
                                        inventoryItem.UUID = itemID;
                                        inventoryItem.AssetUUID = assetID;
                                        succeeded = completed;
                                        UpdateScriptEvent.Set();
                                    });
                                if (!UpdateScriptEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, true))
                                {
                                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                    throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ASSET);
                                }
                            }
                            Locks.ClientInstanceInventoryLock.ExitWriteLock();
                            break;

                        case Enumerations.Type.AGENT:
                            if (!HasCorradePermission(corradeCommandParameters.Group.UUID, (int)Configuration.Permissions.Inventory))
                            {
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                            }
                            // If an item was specified then update instead of creating a new item for certain asset types.
                            if (!string.IsNullOrEmpty(item))
                            {
                                switch (UUID.TryParse(item, out itemUUID))
                                {
                                    case true:
                                        Locks.ClientInstanceInventoryLock.EnterReadLock();
                                        if (Client.Inventory.Store.Contains(itemUUID))
                                        {
                                            inventoryItem = Client.Inventory.Store[itemUUID] as InventoryItem;
                                        }
                                        Locks.ClientInstanceInventoryLock.ExitReadLock();
                                        break;

                                    default:
                                        inventoryItem = Inventory.FindInventory<InventoryBase>(Client, item,
                                            CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                            corradeConfiguration.ServicesTimeout) as InventoryItem;
                                        break;
                                }

                                if (inventoryItem == null || !inventoryItem.AssetType.Equals(AssetType.LSLText))
                                    throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                            }
                            Locks.ClientInstanceInventoryLock.EnterWriteLock();
                            using (var memoryStream = new MemoryStream(Encoding.UTF8.GetBytes(data)))
                            {
                                Client.Inventory.RequestUpdateScriptAgentInventory(memoryStream.ToArray(), inventoryItem.UUID, mono,
                                    delegate (bool completed, string status, bool compiled, List<string> messages,
                                        UUID itemID, UUID assetID)
                                    {
                                        // Add the compiler output to the return.
                                        if (!compiled)
                                            csv.AddRange(new[] {
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ERROR)), CSV.FromEnumerable(messages),
                                            });
                                        inventoryItem.UUID = itemID;
                                        inventoryItem.AssetUUID = assetID;
                                        succeeded = completed;
                                        UpdateScriptEvent.Set();
                                    });
                                if (!UpdateScriptEvent.WaitOne((int)corradeConfiguration.ServicesTimeout, true))
                                {
                                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                                    throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_UPLOADING_ASSET);
                                }
                            }
                            Locks.ClientInstanceInventoryLock.ExitWriteLock();
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_UPDATE_TYPE);
                    }

                    // Add the item and assetUUID ot the output.
                    csv.AddRange(new[]
                        {
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)), inventoryItem.UUID.ToString(),
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ASSET)), inventoryItem.AssetUUID.ToString()
                        });
                    // Return the item and asset UUID.
                    result.Add(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DATA)),
                        CSV.FromEnumerable(csv));
                };
        }
    }
}
