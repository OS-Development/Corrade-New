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

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> notice =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.Name, (int) Configuration.Permissions.Group))
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
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE)),
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
                                KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.SUBJECT)),
                                    corradeCommandParameters.Message)),
                        OwnerID = Client.Self.AgentID
                    };
                    object item =
                        StringOrUUID(
                            wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
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
                        InventoryItem inventoryItem = inventoryBaseItem as InventoryItem;
                        if (inventoryItem == null)
                        {
                            throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                        }
                        // Sending a notice attachment requires copy and transfer permission on the object.
                        if (!inventoryItem.Permissions.OwnerMask.HasFlag(PermissionMask.Copy) ||
                            !inventoryItem.Permissions.OwnerMask.HasFlag(PermissionMask.Transfer))
                        {
                            throw new ScriptException(ScriptError.NO_PERMISSIONS_FOR_ITEM);
                        }
                        // Set requested permissions if any on the item.
                        string permissions = wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.PERMISSIONS)),
                                corradeCommandParameters.Message));
                        if (!string.IsNullOrEmpty(permissions))
                        {
                            if (!wasSetInventoryItemPermissions(inventoryItem, permissions))
                            {
                                throw new ScriptException(ScriptError.SETTING_PERMISSIONS_FAILED);
                            }
                        }
                        notice.AttachmentID = inventoryBaseItem.UUID;
                    }
                    Client.Groups.SendGroupNotice(corradeCommandParameters.Group.UUID, notice);
                };
        }
    }
}