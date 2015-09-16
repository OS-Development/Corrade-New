///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

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
            public static Action<CorradeCommandParameters, Dictionary<string, string>> notice =
                (corradeCommandParameters, result) =>
                {
                    if (!HasCorradePermission(corradeCommandParameters.Group.Name, (int) Permissions.Group))
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
                    if (!new HashSet<UUID>(currentGroups).Contains(corradeCommandParameters.Group.UUID))
                    {
                        throw new ScriptException(ScriptError.NOT_IN_GROUP);
                    }
                    if (
                        !HasGroupPowers(Client.Self.AgentID, corradeCommandParameters.Group.UUID,
                            GroupPowers.SendNotices,
                            corradeConfiguration.ServicesTimeout, corradeConfiguration.DataTimeout))
                    {
                        throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                    }
                    string body =
                        wasInput(
                            wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.MESSAGE)),
                                corradeCommandParameters.Message));
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
                                    corradeCommandParameters.Message)),
                        OwnerID = Client.Self.AgentID
                    };
                    object item =
                        StringOrUUID(
                            wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)),
                                corradeCommandParameters.Message)));
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
                    Client.Groups.SendGroupNotice(corradeCommandParameters.Group.UUID, notice);
                };
        }
    }
}