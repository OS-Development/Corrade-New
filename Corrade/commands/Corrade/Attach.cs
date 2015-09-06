using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using OpenMetaverse;
using Parallel = System.Threading.Tasks.Parallel;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> attach = (commandGroup, message, result) =>
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
                bool replace;
                if (
                    !bool.TryParse(
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.REPLACE)),
                                message)),
                        out replace))
                {
                    replace = true;
                }
                Dictionary<string, string> items =
                    new Dictionary<string, string>(wasCSVToEnumerable(attachments)
                        .AsParallel()
                        .Select((o, p) => new {o, p})
                        .GroupBy(q => q.p/2, q => q.o)
                        .Select(o => o.ToList())
                        .TakeWhile(o => o.Count%2 == 0)
                        .Where(o => !string.IsNullOrEmpty(o.First()) || !string.IsNullOrEmpty(o.Last()))
                        .ToDictionary(o => o.First(), p => p.Last()));
                // if this is SecondLife, check that the additional attachments would not exceed the maximum attachment limit
                if (IsSecondLife())
                {
                    switch (replace)
                    {
                        case true:
                            if (
                                GetAttachments(corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout)
                                    .Count() + items.Count() -
                                typeof (AttachmentPoint).GetFields(
                                    BindingFlags.Public | BindingFlags.Static)
                                    .AsParallel().Count(p => !items.ContainsKey(p.Name)) >
                                LINDEN_CONSTANTS.AVATARS.MAXIMUM_NUMBER_OF_ATTACHMENTS)
                            {
                                throw new Exception(
                                    wasGetDescriptionFromEnumValue(
                                        ScriptError.ATTACHMENTS_WOULD_EXCEED_MAXIMUM_ATTACHMENT_LIMIT));
                            }
                            break;
                        default:
                            if (items.Count +
                                GetAttachments(corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout)
                                    .Count() >
                                LINDEN_CONSTANTS.AVATARS.MAXIMUM_NUMBER_OF_ATTACHMENTS)
                            {
                                throw new Exception(
                                    wasGetDescriptionFromEnumValue(
                                        ScriptError.ATTACHMENTS_WOULD_EXCEED_MAXIMUM_ATTACHMENT_LIMIT));
                            }
                            break;
                    }
                }
                Parallel.ForEach(items, o =>
                    Parallel.ForEach(
                        typeof (AttachmentPoint).GetFields(BindingFlags.Public | BindingFlags.Static)
                            .AsParallel().Where(
                                p =>
                                    p.Name.Equals(o.Key, StringComparison.Ordinal)),
                        q =>
                        {
                            InventoryBase inventoryBaseItem =
                                FindInventory<InventoryBase>(Client.Inventory.Store.RootNode,
                                    StringOrUUID(o.Value)
                                    )
                                    .AsParallel().FirstOrDefault(
                                        r => r is InventoryObject || r is InventoryAttachment);
                            if (inventoryBaseItem == null)
                                return;
                            Attach(inventoryBaseItem as InventoryItem, (AttachmentPoint) q.GetValue(null),
                                replace);
                        }));
                RebakeTimer.Change(corradeConfiguration.RebakeDelay, 0);
            };
        }
    }
}