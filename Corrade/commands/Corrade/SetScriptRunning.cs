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

namespace Corrade
{
    public partial class Corrade
    {
        public static partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> setscriptrunning =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)),
                            out range))
                    {
                        range = corradeConfiguration.Range;
                    }
                    string entity =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ENTITY)),
                                corradeCommandParameters.Message));
                    UUID entityUUID;
                    if (!UUID.TryParse(entity, out entityUUID))
                    {
                        if (string.IsNullOrEmpty(entity))
                        {
                            throw new ScriptException(ScriptError.UNKNOWN_ENTITY);
                        }
                        entityUUID = UUID.Zero;
                    }
                    Primitive primitive = null;
                    string item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                    {
                        throw new ScriptException(ScriptError.NO_ITEM_SPECIFIED);
                    }
                    UUID itemUUID;
                    if (UUID.TryParse(item, out itemUUID))
                    {
                        if (
                            !Services.FindPrimitive(Client,
                                itemUUID,
                                range,
                                corradeConfiguration.Range,
                                ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                        {
                            throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                        }
                    }
                    else
                    {
                        if (
                            !Services.FindPrimitive(Client,
                                item,
                                range,
                                corradeConfiguration.Range,
                                ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout,
                                new Time.DecayingAlarm(corradeConfiguration.DataDecayType)))
                        {
                            throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
                        }
                    }
                    List<InventoryBase> inventory = new List<InventoryBase>();
                    lock (Locks.ClientInstanceInventoryLock)
                    {
                        inventory.AddRange(
                            Client.Inventory.GetTaskInventory(primitive.ID, primitive.LocalID,
                                (int) corradeConfiguration.ServicesTimeout));
                    }
                    InventoryItem inventoryItem = !entityUUID.Equals(UUID.Zero)
                        ? inventory.AsParallel().FirstOrDefault(o => o.UUID.Equals(entityUUID)) as InventoryItem
                        : inventory.AsParallel().FirstOrDefault(o => o.Name.Equals(entity)) as InventoryItem;
                    if (inventoryItem == null)
                    {
                        throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                    }
                    switch (inventoryItem.AssetType)
                    {
                        case AssetType.LSLBytecode:
                        case AssetType.LSLText:
                            break;
                        default:
                            throw new ScriptException(ScriptError.ITEM_IS_NOT_A_SCRIPT);
                    }
                    Action action =
                        Reflection.GetEnumValueFromName<Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant());
                    switch (action)
                    {
                        case Action.START:
                        case Action.STOP:
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.RequestSetScriptRunning(primitive.ID, inventoryItem.UUID,
                                    action.Equals(Action.START));
                            }
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                    ManualResetEvent ScriptRunningReplyEvent = new ManualResetEvent(false);
                    bool succeeded = false;
                    EventHandler<ScriptRunningReplyEventArgs> ScriptRunningEventHandler = (sender, args) =>
                    {
                        switch (action)
                        {
                            case Action.START:
                                succeeded = args.IsRunning;
                                break;
                            case Action.STOP:
                                succeeded = !args.IsRunning;
                                break;
                        }
                        ScriptRunningReplyEvent.Set();
                    };
                    lock (Locks.ClientInstanceInventoryLock)
                    {
                        Client.Inventory.ScriptRunningReply += ScriptRunningEventHandler;
                        Client.Inventory.RequestGetScriptRunning(primitive.ID, inventoryItem.UUID);
                        if (!ScriptRunningReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                        {
                            Client.Inventory.ScriptRunningReply -= ScriptRunningEventHandler;
                            throw new ScriptException(ScriptError.TIMEOUT_GETTING_SCRIPT_STATE);
                        }
                        Client.Inventory.ScriptRunningReply -= ScriptRunningEventHandler;
                    }
                    if (!succeeded)
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_SET_SCRIPT_STATE);
                    }
                };
        }
    }
}