///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<Group, string, Dictionary<string, string>> addpick = (commandGroup, message, result) =>
            {
                if (
                    !HasCorradePermission(commandGroup.Name,
                        (int) Permissions.Grooming))
                {
                    throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                }
                object item =
                    StringOrUUID(
                        wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.ITEM)),
                            message)));
                UUID textureUUID = UUID.Zero;
                if (item != null)
                {
                    InventoryBase inventoryBaseItem =
                        FindInventory<InventoryBase>(Client.Inventory.Store.RootNode, item
                            ).FirstOrDefault();
                    if (inventoryBaseItem == null)
                    {
                        throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                    }
                    textureUUID = inventoryBaseItem.UUID;
                }
                ManualResetEvent AvatarPicksReplyEvent = new ManualResetEvent(false);
                UUID pickUUID = UUID.Zero;
                string name =
                    wasInput(wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.NAME)),
                        message));
                if (string.IsNullOrEmpty(name))
                {
                    throw new ScriptException(ScriptError.EMPTY_PICK_NAME);
                }
                int pickCount = 0;
                EventHandler<AvatarPicksReplyEventArgs> AvatarPicksEventHandler = (sender, args) =>
                {
                    pickCount = args.Picks.Count;
                    KeyValuePair<UUID, string> pick =
                        args.Picks.AsParallel()
                            .FirstOrDefault(o => o.Value.Equals(name, StringComparison.Ordinal));
                    if (!pick.Equals(default(KeyValuePair<UUID, string>)))
                        pickUUID = pick.Key;
                    AvatarPicksReplyEvent.Set();
                };
                lock (ClientInstanceAvatarsLock)
                {
                    Client.Avatars.AvatarPicksReply += AvatarPicksEventHandler;
                    Client.Avatars.RequestAvatarPicks(Client.Self.AgentID);
                    if (!AvatarPicksReplyEvent.WaitOne((int) corradeConfiguration.ServicesTimeout, false))
                    {
                        Client.Avatars.AvatarPicksReply -= AvatarPicksEventHandler;
                        throw new ScriptException(ScriptError.TIMEOUT_GETTING_PICKS);
                    }
                    Client.Avatars.AvatarPicksReply -= AvatarPicksEventHandler;
                }
                string description =
                    wasInput(
                        wasKeyValueGet(wasOutput(wasGetDescriptionFromEnumValue(ScriptKeys.DESCRIPTION)),
                            message));
                if (IsSecondLife())
                {
                    if (pickUUID.Equals(UUID.Zero) &&
                        pickCount >= LINDEN_CONSTANTS.AVATARS.PICKS.MAXIMUM_PICKS)
                    {
                        throw new ScriptException(ScriptError.MAXIMUM_AMOUNT_OF_PICKS_REACHED);
                    }
                    if (Encoding.UTF8.GetByteCount(description) >
                        LINDEN_CONSTANTS.AVATARS.PICKS.MAXIMUM_PICK_DESCRIPTION_SIZE)
                    {
                        throw new ScriptException(ScriptError.DESCRIPTION_WOULD_EXCEED_MAXIMUM_SIZE);
                    }
                }
                if (pickUUID.Equals(UUID.Zero))
                {
                    pickUUID = UUID.Random();
                }
                Client.Self.PickInfoUpdate(pickUUID, false, UUID.Zero, name,
                    Client.Self.GlobalPosition, textureUUID, description);
            };
        }
    }
}