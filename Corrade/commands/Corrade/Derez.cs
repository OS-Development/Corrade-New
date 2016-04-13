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
using wasOpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> derez =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Inventory))
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
                    string folder = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FOLDER)),
                            corradeCommandParameters.Message));
                    InventoryFolder inventoryFolder;
                    switch (!string.IsNullOrEmpty(folder))
                    {
                        case true:
                            UUID folderUUID;
                            if (UUID.TryParse(folder, out folderUUID))
                            {
                                InventoryBase inventoryBaseItem =
                                    Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                        folderUUID
                                        ).FirstOrDefault();
                                if (inventoryBaseItem == null)
                                {
                                    throw new ScriptException(ScriptError.FOLDER_NOT_FOUND);
                                }
                                inventoryFolder = inventoryBaseItem as InventoryFolder;
                            }
                            else
                            {
                                // attempt regex and then fall back to string
                                InventoryBase inventoryBaseItem = null;
                                try
                                {
                                    inventoryBaseItem =
                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                            new Regex(folder, RegexOptions.Compiled | RegexOptions.IgnoreCase))
                                            .FirstOrDefault();
                                }
                                catch (Exception)
                                {
                                    // not a regex so we do not care
                                    inventoryBaseItem =
                                        Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode,
                                            folder)
                                            .FirstOrDefault();
                                }
                                if (inventoryBaseItem == null)
                                {
                                    throw new ScriptException(ScriptError.FOLDER_NOT_FOUND);
                                }
                                inventoryFolder = inventoryBaseItem as InventoryFolder;
                            }
                            if (inventoryFolder == null)
                            {
                                throw new ScriptException(ScriptError.FOLDER_NOT_FOUND);
                            }
                            break;
                        default:
                            inventoryFolder =
                                Client.Inventory.Store.Items[Client.Inventory.FindFolderForType(AssetType.Object)]
                                    .Data as InventoryFolder;
                            break;
                    }
                    FieldInfo deRezDestionationTypeInfo = typeof (DeRezDestination).GetFields(BindingFlags.Public |
                                                                                              BindingFlags.Static)
                        .AsParallel().FirstOrDefault(
                            o =>
                                o.Name.Equals(
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.TYPE)),
                                            corradeCommandParameters.Message)),
                                    StringComparison.Ordinal));
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
                    Client.Inventory.RequestDeRezToInventory(primitive.LocalID, deRezDestionationTypeInfo != null
                        ? (DeRezDestination)
                            deRezDestionationTypeInfo
                                .GetValue(null)
                        : DeRezDestination.AgentInventoryTake, inventoryFolder.UUID, UUID.Random());
                };
        }
    }
}