///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> setscriptrunning
                =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Interact))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)),
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
                    var item = wasInput(KeyValue.Get(
                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                        corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(item))
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
                    }
                    UUID itemUUID;
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
                    lock (Locks.ClientInstanceInventoryLock)
                    {
                        inventory.AddRange(
                            Client.Inventory.GetTaskInventory(primitive.ID, primitive.LocalID,
                                (int) corradeConfiguration.ServicesTimeout));
                    }
                    var inventoryItem = !entityUUID.Equals(UUID.Zero)
                        ? inventory.AsParallel().FirstOrDefault(o => o.UUID.Equals(entityUUID)) as InventoryItem
                        : inventory.AsParallel().FirstOrDefault(o => o.Name.Equals(entity)) as InventoryItem;
                    if (inventoryItem == null)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                    }
                    switch (inventoryItem.AssetType)
                    {
                        case AssetType.LSLBytecode:
                        case AssetType.LSLText:
                            break;
                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.ITEM_IS_NOT_A_SCRIPT);
                    }
                    var action =
                        Reflection.GetEnumValueFromName<Enumerations.Action>(
                            wasInput(
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                    corradeCommandParameters.Message))
                                .ToLowerInvariant());
                    switch (action)
                    {
                        case Enumerations.Action.START:
                        case Enumerations.Action.STOP:
                            lock (Locks.ClientInstanceInventoryLock)
                            {
                                Client.Inventory.RequestSetScriptRunning(primitive.ID, inventoryItem.UUID,
                                    action.Equals(Enumerations.Action.START));
                            }
                            break;
                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                    var ScriptRunningReplyEvent = new ManualResetEvent(false);
                    var succeeded = false;
                    EventHandler<ScriptRunningReplyEventArgs> ScriptRunningEventHandler = (sender, args) =>
                    {
                        switch (action)
                        {
                            case Enumerations.Action.START:
                                succeeded = args.IsRunning;
                                break;
                            case Enumerations.Action.STOP:
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
                            throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_SCRIPT_STATE);
                        }
                        Client.Inventory.ScriptRunningReply -= ScriptRunningEventHandler;
                    }
                    if (!succeeded)
                    {
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_SET_SCRIPT_STATE);
                    }
                };
        }
    }
}