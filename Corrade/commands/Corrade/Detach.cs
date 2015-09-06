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
            public static Action<Group, string, Dictionary<string, string>> detach = (commandGroup, message, result) =>
            {
                if (
                    !HasCorradePermission(commandGroup.Name,
                        (int) Permissions.Grooming))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                string attachments =
                    wasInput(
                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ATTACHMENTS)),
                            message));
                if (string.IsNullOrEmpty(attachments))
                {
                    throw new ScriptException(ScriptError.EMPTY_ATTACHMENTS);
                }
                Parallel.ForEach(wasCSVToEnumerable(
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