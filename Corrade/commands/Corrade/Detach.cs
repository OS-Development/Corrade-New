///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using CorradeConfiguration;
using OpenMetaverse;
using wasSharp;
using Parallel = System.Threading.Tasks.Parallel;

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
                        !HasCorradePermission(corradeCommandParameters.Group.Name,
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
                    Parallel.ForEach(CSV.ToEnumerable(
                        attachments).AsParallel().Where(o => !string.IsNullOrEmpty(o)), o =>
                        {
                            InventoryBase inventoryBaseItem =
                                FindInventory<InventoryBase>(Client.Inventory.Store.RootNode, StringOrUUID(o)
                                    )
                                    .AsParallel().FirstOrDefault(
                                        p =>
                                            p is InventoryObject || p is InventoryAttachment);
                            if (inventoryBaseItem == null)
                                return;
                            Detach(inventoryBaseItem as InventoryItem);
                        });
                    RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
                };
        }
    }
}