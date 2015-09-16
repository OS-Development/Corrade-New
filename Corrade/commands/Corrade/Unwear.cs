///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> unwear =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Grooming))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    string wearables =
                        wasInput(wasKeyValueGet(
                            wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.WEARABLES)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(wearables))
                    {
                        throw new ScriptException(ScriptError.EMPTY_WEARABLES);
                    }
                    Parallel.ForEach(wasCSVToEnumerable(
                        wearables).AsParallel().Where(o => !string.IsNullOrEmpty(o)), o =>
                        {
                            InventoryBase inventoryBaseItem =
                                FindInventory<InventoryBase>(Client.Inventory.Store.RootNode, StringOrUUID(o)
                                    ).AsParallel().FirstOrDefault(p => p is InventoryWearable);
                            if (inventoryBaseItem == null)
                                return;
                            UnWear(inventoryBaseItem as InventoryItem);
                        });
                    RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                };
        }
    }
}