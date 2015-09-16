///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> derez =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Inventory))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    float range;
                    if (
                        !float.TryParse(
                            wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.RANGE)),
                                corradeCommandParameters.Message)),
                            out range))
                    {
                        range = corradeConfiguration.Range;
                    }
                    object folder =
                        StringOrUUID(
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.FOLDER)),
                                corradeCommandParameters.Message)));
                    InventoryFolder inventoryFolder;
                    switch (folder != null)
                    {
                        case true:
                            InventoryBase inventoryBaseItem =
                                FindInventory<InventoryBase>(Client.Inventory.Store.RootNode, folder
                                    ).FirstOrDefault();
                            if (inventoryBaseItem == null)
                            {
                                throw new ScriptException(ScriptError.FOLDER_NOT_FOUND);
                            }
                            inventoryFolder = inventoryBaseItem as InventoryFolder;
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
                                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.TYPE)),
                                            corradeCommandParameters.Message)),
                                    StringComparison.Ordinal));
                    Primitive primitive = null;
                    if (
                        !FindPrimitive(
                            StringOrUUID(wasInput(wasKeyValueGet(
                                wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)),
                                corradeCommandParameters.Message))),
                            range,
                            ref primitive, corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                    {
                        throw new ScriptException(ScriptError.PRIMITIVE_NOT_FOUND);
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