///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
using Corrade.Constants;
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
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> derez =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)), NumberStyles.Float, Utils.EnUsCulture,
                            out range))
                        range = corradeConfiguration.Range;
                    var folder = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FOLDER)),
                            corradeCommandParameters.Message));
                    InventoryFolder inventoryFolder = null;
                    switch (!string.IsNullOrEmpty(folder))
                    {
                        case true:
                            UUID folderUUID;
                            switch (UUID.TryParse(folder, out folderUUID))
                            {
                                case true:
                                    Locks.ClientInstanceInventoryLock.EnterReadLock();
                                    if (Client.Inventory.Store.Contains(folderUUID))
                                        inventoryFolder = Client.Inventory.Store[folderUUID] as InventoryFolder;
                                    Locks.ClientInstanceInventoryLock.ExitReadLock();
                                    break;

                                default:
                                    inventoryFolder =
                                        Inventory.FindInventory<InventoryFolder>(Client, folder,
                                            CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                            corradeConfiguration.ServicesTimeout);
                                    break;
                            }
                            if (inventoryFolder == null)
                                throw new Command.ScriptException(Enumerations.ScriptError.FOLDER_NOT_FOUND);
                            break;

                        default:
                            Locks.ClientInstanceInventoryLock.EnterReadLock();
                            inventoryFolder =
                                Client.Inventory.Store.Items[Client.Inventory.FindFolderForType(AssetType.Object)]
                                    .Data as InventoryFolder;
                            Locks.ClientInstanceInventoryLock.ExitReadLock();
                            break;
                    }
                    var deRezDestionationTypeInfo = typeof(DeRezDestination).GetFields(BindingFlags.Public |
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
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_ITEM_SPECIFIED);
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
                    Locks.ClientInstanceInventoryLock.EnterWriteLock();
                    Client.Inventory.RequestDeRezToInventory(primitive.LocalID, deRezDestionationTypeInfo != null
                        ? (DeRezDestination)
                        deRezDestionationTypeInfo
                            .GetValue(null)
                        : DeRezDestination.AgentInventoryTake, inventoryFolder.UUID, UUID.Random());
                    Locks.ClientInstanceInventoryLock.ExitWriteLock();
                };
        }
    }
}