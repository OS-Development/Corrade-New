///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using CorradeConfiguration;
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
            public static Action<Command.CorradeCommandParameters, Dictionary<string, string>> derez =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
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
                    var folder = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FOLDER)),
                            corradeCommandParameters.Message));
                    InventoryFolder inventoryFolder;
                    switch (!string.IsNullOrEmpty(folder))
                    {
                        case true:
                            UUID folderUUID;
                            switch (UUID.TryParse(folder, out folderUUID))
                            {
                                case true:
                                    inventoryFolder =
                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                            folderUUID, corradeConfiguration.ServicesTimeout
                                            ).FirstOrDefault() as InventoryFolder;
                                    break;
                                default:
                                    inventoryFolder =
                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                            folder, corradeConfiguration.ServicesTimeout)
                                            .FirstOrDefault() as InventoryFolder;
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
                                    Client.Inventory.Store.Items[Client.Inventory.FindFolderForType(AssetType.Object)]
                                        .Data as InventoryFolder;
                            }
                            break;
                    }
                    var deRezDestionationTypeInfo = typeof (DeRezDestination).GetFields(BindingFlags.Public |
                                                                                        BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TYPE)),
                                            corradeCommandParameters.Message)),
                                    StringComparison.Ordinal));
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
                    lock (Locks.ClientInstanceInventoryLock)
                    {
                        Client.Inventory.RequestDeRezToInventory(primitive.LocalID, deRezDestionationTypeInfo != null
                            ? (DeRezDestination)
                                deRezDestionationTypeInfo
                                    .GetValue(null)
                            : DeRezDestination.AgentInventoryTake, inventoryFolder.UUID, UUID.Random());
                    }
                };
        }
    }
}