using System;
using System.Collections.Generic;
using System.Linq;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> notice = (commandGroup, message, result) =>
            {
                if (!HasCorradePermission(commandGroup.Name, (int) Permissions.Group))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                IEnumerable<UUID> currentGroups = Enumerable.Empty<UUID>();
                if (
                    !GetCurrentGroups(corradeConfiguration.ServicesTimeout,
                        ref currentGroups))
                {
                    throw new ScriptException(ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                }
                if (!currentGroups.ToList().Any(o => o.Equals(commandGroup.UUID)))
                {
                    throw new ScriptException(ScriptError.NOT_IN_GROUP);
                }
                if (
                    !HasGroupPowers(Client.Self.AgentID, commandGroup.UUID, GroupPowers.SendNotices,
                        corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                {
                    throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                }
                string body =
                    wasInput(
                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.MESSAGE)),
                            message));
                if (IsSecondLife() && body.Length > LINDEN_CONSTANTS.NOTICES.MAXIMUM_NOTICE_MESSAGE_LENGTH)
                {
                    throw new ScriptException(ScriptError.TOO_MANY_CHARACTERS_FOR_NOTICE_MESSAGE);
                }
                GroupNotice notice = new GroupNotice
                {
                    Message = body,
                    Subject =
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.SUBJECT)),
                                message)),
                    OwnerID = Client.Self.AgentID
                };
                object item =
                    StringOrUUID(
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)),
                            message)));
                if (item != null)
                {
                    InventoryBase inventoryBaseItem =
                        FindInventory<InventoryBase>(Client.Inventory.Store.RootNode, item
                            ).FirstOrDefault();
                    if (inventoryBaseItem == null)
                    {
                        throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                    }
                    notice.AttachmentID = inventoryBaseItem.UUID;
                }
                Client.Groups.SendGroupNotice(commandGroup.UUID, notice);
            };
        }
    }
}