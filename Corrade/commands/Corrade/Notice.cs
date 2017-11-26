///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Corrade.Constants;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using wasSharp.Timers;
using Inventory = wasOpenMetaverse.Inventory;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> notice =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Group))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    UUID groupUUID;
                    var target = wasInput(
                        KeyValue.Get(
                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.TARGET)),
                            corradeCommandParameters.Message));
                    switch (string.IsNullOrEmpty(target))
                    {
                        case false:
                            if (!UUID.TryParse(target, out groupUUID) &&
                                !Resolvers.GroupNameToUUID(Client, target, corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout,
                                    new DecayingAlarm(corradeConfiguration.DataDecayType), ref groupUUID))
                                throw new Command.ScriptException(Enumerations.ScriptError.GROUP_NOT_FOUND);
                            break;

                        default:
                            groupUUID = corradeCommandParameters.Group.UUID;
                            break;
                    }
                    var currentGroups = Enumerable.Empty<UUID>();
                    if (
                        !Services.GetCurrentGroups(Client, corradeConfiguration.ServicesTimeout,
                            ref currentGroups))
                        throw new Command.ScriptException(Enumerations.ScriptError.COULD_NOT_GET_CURRENT_GROUPS);
                    if (!new HashSet<UUID>(currentGroups).Contains(groupUUID))
                        throw new Command.ScriptException(Enumerations.ScriptError.NOT_IN_GROUP);
                    var action = Reflection.GetEnumValueFromName<Enumerations.Action>(
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ACTION)),
                                corradeCommandParameters.Message))
                    );
                    switch (action)
                    {
                        case Enumerations.Action.SEND:
                            if (
                                !Services.HasGroupPowers(Client, Client.Self.AgentID,
                                    groupUUID,
                                    GroupPowers.SendNotices, corradeConfiguration.ServicesTimeout,
                                    corradeConfiguration.DataTimeout,
                                    new DecayingAlarm(corradeConfiguration.DataDecayType)))
                                throw new Command.ScriptException(Enumerations.ScriptError.NO_GROUP_POWER_FOR_COMMAND);
                            var body =
                                wasInput(
                                    KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.MESSAGE)),
                                        corradeCommandParameters.Message));
                            if (wasOpenMetaverse.Helpers.IsSecondLife(Client) &&
                                body.Length > wasOpenMetaverse.Constants.NOTICES.MAXIMUM_NOTICE_MESSAGE_LENGTH)
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TOO_MANY_CHARACTERS_FOR_NOTICE_MESSAGE);
                            var notice = new GroupNotice
                            {
                                Message = body,
                                Subject =
                                    wasInput(
                                        KeyValue.Get(
                                            wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SUBJECT)),
                                            corradeCommandParameters.Message)),
                                OwnerID = Client.Self.AgentID
                            };
                            var item = wasInput(
                                KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                                    corradeCommandParameters.Message));
                            if (!string.IsNullOrEmpty(item))
                            {
                                InventoryItem inventoryItem = null;
                                UUID itemUUID;
                                switch (UUID.TryParse(item, out itemUUID))
                                {
                                    case true:
                                        Locks.ClientInstanceInventoryLock.EnterReadLock();
                                        if (Client.Inventory.Store.Contains(itemUUID))
                                            inventoryItem = Client.Inventory.Store[itemUUID] as InventoryItem;
                                        Locks.ClientInstanceInventoryLock.ExitReadLock();
                                        break;

                                    default:
                                        inventoryItem =
                                            Inventory.FindInventory<InventoryItem>(Client, item,
                                                CORRADE_CONSTANTS.PATH_SEPARATOR,
                                                CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                                corradeConfiguration.ServicesTimeout);
                                        break;
                                }
                                if (inventoryItem == null)
                                    throw new Command.ScriptException(Enumerations.ScriptError
                                        .INVENTORY_ITEM_NOT_FOUND);
                                // Sending a notice attachment requires copy and transfer permission on the object.
                                if (!inventoryItem.Permissions.OwnerMask.HasFlag(PermissionMask.Copy) ||
                                    !inventoryItem.Permissions.OwnerMask.HasFlag(PermissionMask.Transfer))
                                    throw new Command.ScriptException(Enumerations.ScriptError.NO_PERMISSIONS_FOR_ITEM);
                                // Set requested permissions if any on the item.
                                var permissions = wasInput(
                                    KeyValue.Get(
                                        wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.PERMISSIONS)),
                                        corradeCommandParameters.Message));
                                if (!string.IsNullOrEmpty(permissions))
                                {
                                    Inventory.wasStringToPermissions(permissions, out inventoryItem.Permissions);
                                    Client.Inventory.RequestUpdateItem(inventoryItem);
                                }
                                notice.AttachmentID = inventoryItem.UUID;
                            }
                            Client.Groups.SendGroupNotice(groupUUID, notice);
                            break;

                        case Enumerations.Action.LIST:
                            var GroupNoticesReplyEvent = new ManualResetEventSlim(false);
                            var groupNotices = new List<GroupNoticesListEntry>();
                            EventHandler<GroupNoticesListReplyEventArgs> GroupNoticesListEventHandler =
                                (sender, args) =>
                                {
                                    if (!args.GroupID.Equals(groupUUID))
                                        return;

                                    groupNotices.AddRange(args.Notices.OrderBy(o => o.Timestamp));
                                    GroupNoticesReplyEvent.Set();
                                };
                            Locks.ClientInstanceGroupsLock.EnterReadLock();
                            Client.Groups.GroupNoticesListReply += GroupNoticesListEventHandler;
                            Client.Groups.RequestGroupNoticesList(groupUUID);
                            if (!GroupNoticesReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                            {
                                Client.Groups.GroupNoticesListReply -= GroupNoticesListEventHandler;
                                Locks.ClientInstanceGroupsLock.ExitReadLock();
                                throw new Command.ScriptException(
                                    Enumerations.ScriptError.TIMEOUT_RETRIEVING_GROUP_NOTICES);
                            }
                            Client.Groups.GroupNoticesListReply -= GroupNoticesListEventHandler;
                            Locks.ClientInstanceGroupsLock.ExitReadLock();
                            var csv = new List<string>();
                            var LockObject = new object();
                            groupNotices.AsParallel().ForAll(o =>
                            {
                                lock (LockObject)
                                {
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.ASSET),
                                        o.HasAttachment ? o.AssetType.ToString() : AssetType.Unknown.ToString()
                                    });
                                    csv.AddRange(new[]
                                        {Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME), o.FromName});
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.ATTACHMENTS),
                                        o.HasAttachment.ToString()
                                    });
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.NOTICE),
                                        o.NoticeID.ToString()
                                    });
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.SUBJECT),
                                        o.Subject
                                    });
                                    csv.AddRange(new[]
                                    {
                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.TIME),
                                        o.Timestamp.ToString()
                                    });
                                }
                            });
                            if (groupNotices.Any())
                                result.Add(Reflection.GetNameFromEnumValue(Command.ResultKeys.DATA),
                                    CSV.FromEnumerable(csv));
                            break;

                        case Enumerations.Action.ACCEPT:
                        case Enumerations.Action.DECLINE:
                            UUID agentUUID;
                            UUID sessionUUID;
                            UUID folderUUID;
                            UUID groupNotice;
                            switch (UUID.TryParse(
                                wasInput(KeyValue.Get(
                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NOTICE)),
                                    corradeCommandParameters.Message)),
                                out groupNotice))
                            {
                                case true:
                                    var InstantMessageEvent = new ManualResetEventSlim(false);
                                    var instantMessage = new InstantMessage();
                                    EventHandler<InstantMessageEventArgs> InstantMessageEventHandler =
                                        (sender, args) =>
                                        {
                                            // Abort if this is not a request for a group notice or if the
                                            // request for a group notice is not for the current command group.
                                            if (!args.IM.Dialog.Equals(InstantMessageDialog.GroupNoticeRequested) ||
                                                !groupUUID.Equals(
                                                    args.IM.BinaryBucket.Length > 18
                                                        ? new UUID(args.IM.BinaryBucket, 2)
                                                        : args.IM.FromAgentID))
                                                return;
                                            instantMessage = args.IM;
                                            InstantMessageEvent.Set();
                                        };
                                    Client.Self.IM += InstantMessageEventHandler;
                                    Client.Groups.RequestGroupNotice(groupNotice);
                                    if (
                                        !InstantMessageEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                                    {
                                        Client.Self.IM -= InstantMessageEventHandler;
                                        throw new Command.ScriptException(
                                            Enumerations.ScriptError.TIMEOUT_RETRIEVING_NOTICE);
                                    }
                                    Client.Self.IM -= InstantMessageEventHandler;
                                    if (instantMessage.Equals(default(InstantMessage)))
                                        throw new Command.ScriptException(Enumerations.ScriptError.NO_NOTICE_FOUND);
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
                                            throw new Command.ScriptException(
                                                Enumerations.ScriptError.NOTICE_DOES_NOT_CONTAIN_ATTACHMENT);
                                    }
                                    break;

                                default:
                                    if (
                                        !UUID.TryParse(
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys
                                                        .AGENT)),
                                                    corradeCommandParameters.Message)), out agentUUID) &&
                                        !Resolvers.AgentNameToUUID(Client,
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(
                                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.FIRSTNAME)),
                                                    corradeCommandParameters.Message)),
                                            wasInput(
                                                KeyValue.Get(
                                                    wasOutput(
                                                        Reflection.GetNameFromEnumValue(Command.ScriptKeys.LASTNAME)),
                                                    corradeCommandParameters.Message)),
                                            corradeConfiguration.ServicesTimeout,
                                            corradeConfiguration.DataTimeout,
                                            new DecayingAlarm(corradeConfiguration.DataDecayType),
                                            ref agentUUID))
                                        throw new Command.ScriptException(Enumerations.ScriptError.AGENT_NOT_FOUND);
                                    if (!UUID.TryParse(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.SESSION)),
                                                corradeCommandParameters.Message)), out sessionUUID))
                                        throw new Command.ScriptException(Enumerations.ScriptError
                                            .NO_SESSION_SPECIFIED);
                                    if (!UUID.TryParse(
                                        wasInput(
                                            KeyValue.Get(
                                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.FOLDER)),
                                                corradeCommandParameters.Message)), out folderUUID))
                                        throw new Command.ScriptException(Enumerations.ScriptError
                                            .NO_SESSION_SPECIFIED);
                                    break;
                            }
                            Locks.ClientInstanceSelfLock.EnterWriteLock();
                            Client.Self.InstantMessage(Client.Self.Name, agentUUID, string.Empty,
                                sessionUUID,
                                action.Equals(Enumerations.Action.ACCEPT)
                                    ? InstantMessageDialog.GroupNoticeInventoryAccepted
                                    : InstantMessageDialog.GroupNoticeInventoryDeclined,
                                InstantMessageOnline.Offline,
                                Client.Self.SimPosition, Client.Network.CurrentSim.RegionID, folderUUID.GetBytes());
                            Locks.ClientInstanceSelfLock.ExitWriteLock();
                            break;

                        default:
                            throw new Command.ScriptException(Enumerations.ScriptError.UNKNOWN_ACTION);
                    }
                };
        }
    }
}