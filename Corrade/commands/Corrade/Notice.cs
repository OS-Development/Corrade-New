///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Helpers = wasOpenMetaverse.Helpers;

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
                        !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                    {
                        throw new ScriptException(ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                    }
                    if (!new HashSet<UUID>(currentGroups).Contains(corradeCommandParameters.Group.UUID))
                    {
                        throw new ScriptException(ScriptError.NOT_IN_GROUP);
                    }
                    Action action = Reflection.GetEnumValueFromName<Action>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                            .ToLowerInvariant());
                    switch (action)
                    {
                        case Action.SEND:
                            if (
                                !HasGroupPowers(Client.Self.AgentID, corradeCommandParameters.Group.UUID,
                                    GroupPowers.SendNotices, corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout))
                            {
                                throw new ScriptException(ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                            }
                            string body =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.MESSAGE)),
                                        corradeCommandParameters.Message));
                            if (IsSecondLife() && body.Length > Constants.NOTICES.MAXIMUM_NOTICE_MESSAGE_LENGTH)
                            {
                                throw new ScriptException(ScriptError.TOO_MANY_CHARACTERS_FOR_NOTICE_MESSAGE);
                            }
                            OpenMetaverse.GroupNotice notice = new OpenMetaverse.GroupNotice
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
                                Helpers.StringOrUUID(
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
                            break;
                        case Action.LIST:
                            ManualResetEvent GroupNoticesReplyEvent = new ManualResetEvent(false);
                            List<GroupNoticesListEntry> groupNotices = new List<GroupNoticesListEntry>();
                            EventHandler<GroupNoticesListReplyEventArgs> GroupNoticesListEventHandler =
                                (sender, args) =>
                                {
                                    groupNotices.AddRange(args.Notices.OrderBy(o => o.Timestamp));
                                    GroupNoticesReplyEvent.Set();
                                };
                            lock (Locks.ClientInstanceGroupsLock)
                            {
                                Client.Groups.GroupNoticesListReply += GroupNoticesListEventHandler;
                                Client.Groups.RequestGroupNoticesList(corradeCommandParameters.Group.UUID);
                                if (!GroupNoticesReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                                {
                                    Client.Groups.GroupNoticesListReply -= GroupNoticesListEventHandler;
                                    throw new ScriptException(ScriptError.TIMEOUT_RETRIEVING_GROUP_NOTICES);
                                }
                                Client.Groups.GroupNoticesListReply -= GroupNoticesListEventHandler;
                            }
                            List<string> csv = new List<string>();
                            object LockObject = new object();
                            Parallel.ForEach(groupNotices, o =>
                            {
                                lock (LockObject)
                                {
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetNameFromEnumValue(ScriptKeys.ASSET),
                                        o.HasAttachment ? o.AssetType.ToString() : AssetType.Unknown.ToString()
                                    });
                                    csv.AddRange(new[]
                                    {Reflection.GetNameFromEnumValue(ScriptKeys.NAME), o.FromName});
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetNameFromEnumValue(ScriptKeys.ATTACHMENTS),
                                        o.HasAttachment.ToString()
                                    });
                                    csv.AddRange(new[]
                                    {Reflection.GetNameFromEnumValue(ScriptKeys.NOTICE), o.NoticeID.ToString()});
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetNameFromEnumValue(ScriptKeys.SUBJECT),
                                        o.Subject
                                    });
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetNameFromEnumValue(ScriptKeys.TIME),
                                        o.Timestamp.ToString()
                                    });
                                }
                            });
                            if (groupNotices.Any())
                            {
                                result.Add(Reflection.GetNameFromEnumValue(ResultKeys.DATA), CSV.FromEnumerable(csv));
                            }
                            break;
                        case Action.ACCEPT:
                        case Action.DECLINE:
                            UUID agentUUID;
                            UUID sessionUUID;
                            UUID folderUUID;
                            UUID groupNotice;
                            switch (UUID.TryParse(
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.NOTICE)),
                                    corradeCommandParameters.Message)),
                                out groupNotice))
                            {
                                case true:
                                    ManualResetEvent InstantMessageEvent = new ManualResetEvent(false);
                                    OpenMetaverse.InstantMessage instantMessage = new OpenMetaverse.InstantMessage();
                                    EventHandler<InstantMessageEventArgs> InstantMessageEventHandler =
                                        (sender, args) =>
                                        {
                                            // Abort if this is not a request for a group notice or if the
                                            // request for a group notice is not for the current command group.
                                            if (!args.IM.Dialog.Equals(InstantMessageDialog.GroupNoticeRequested) ||
                                                !corradeCommandParameters.Group.UUID.Equals(
                                                    args.IM.BinaryBucket.Length > 18
                                                        ? new UUID(args.IM.BinaryBucket, 2)
                                                        : args.IM.FromAgentID))
                                                return;
                                            instantMessage = args.IM;
                                            InstantMessageEvent.Set();
                                        };
                                    lock (Locks.ClientInstanceGroupsLock)
                                    {
                                        Client.Self.IM += InstantMessageEventHandler;
                                        Client.Groups.RequestGroupNotice(groupNotice);
                                        if (
                                            !InstantMessageEvent.WaitOne((int) corradeConfiguration.ServicesTimeout,
                                                false))
                                        {
                                            Client.Self.IM -= InstantMessageEventHandler;
                                            throw new ScriptException(ScriptError.TIMEOUT_RETRIEVING_NOTICE);
                                        }
                                        Client.Self.IM -= InstantMessageEventHandler;
                                    }
                                    if (instantMessage.Equals(default(OpenMetaverse.InstantMessage)))
                                    {
                                        throw new ScriptException(ScriptError.NO_NOTICE_FOUND);
                                    }
                                    agentUUID = instantMessage.FromAgentID;
                                    sessionUUID = instantMessage.IMSessionID;
                                    // if the message contains an attachment, retrieve the folder, otherwise, abort
                                    switch (
                                        instantMessage.BinaryBucket.Length > 18 &&
                                        !instantMessage.BinaryBucket[0].Equals(0))
                                    {
                                        case true:
                                            folderUUID =
                                                Client.Inventory.FindFolderForType(
                                                    (AssetType) instantMessage.BinaryBucket[1]);
                                            break;
                                        default:
                                            throw new ScriptException(ScriptError.NOTICE_DOES_NOT_CONTAIN_ATTACHMENT);
                                    }
                                    break;
                                default:
                                    if (
                                        !UUID.TryParse(
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.AGENT)),
                                                    corradeCommandParameters.Message)), out agentUUID) &&
                                        !Resolvers.AgentNameToUUID(Client,
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(
                                                        Reflection.GetNameFromEnumValue(ScriptKeys.FIRSTNAME)),
                                                    corradeCommandParameters.Message)),
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.LASTNAME)),
                                                    corradeCommandParameters.Message)),
                                            corradeConfiguration.ServicesTimeout,
                                            corradeConfiguration.DataTimeout,
                                            new Time.DecayingAlarm(corradeConfiguration.DataDecayType),
                                            ref agentUUID))
                                    {
                                        throw new ScriptException(ScriptError.AGENT_NOT_FOUND);
                                    }
                                    if (!UUID.TryParse(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.SESSION)),
                                                corradeCommandParameters.Message)), out sessionUUID))
                                    {
                                        throw new ScriptException(ScriptError.NO_SESSION_SPECIFIED);
                                    }
                                    if (!UUID.TryParse(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.FOLDER)),
                                                corradeCommandParameters.Message)), out folderUUID))
                                    {
                                        throw new ScriptException(ScriptError.NO_SESSION_SPECIFIED);
                                    }
                                    break;
                            }
                            Client.Self.InstantMessage(Client.Self.Name, agentUUID, string.Empty,
                                sessionUUID,
                                action.Equals(Action.ACCEPT)
                                    ? InstantMessageDialog.GroupNoticeInventoryAccepted
                                    : InstantMessageDialog.GroupNoticeInventoryDeclined, InstantMessageOnline.Offline,
                                Client.Self.SimPosition, Client.Network.CurrentSim.RegionID, folderUUID.GetBytes());
                            break;
                        default:
                            throw new ScriptException(ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}