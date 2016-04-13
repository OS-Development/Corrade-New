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
using CorradeConfiguration;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Helpers = wasOpenMetaverse.Helpers;
using Inventory = wasOpenMetaverse.Inventory;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static Action<CorradeCommandParameters, Dictionary<string, string>> addpick =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                    {
                        throw new ScriptException(ScriptError.NO_CORRADE_PERMISSIONS);
                    }
                    Vector3d position;
                    if (
                        !Vector3d.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.POSITION)),
                                corradeCommandParameters.Message)),
                            out position))
                    {
                        position = Client.Self.GlobalPosition;
                    }
                    string item =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.ITEM)),
                                corradeCommandParameters.Message));
                    UUID textureUUID = UUID.Zero;
                    if (!string.IsNullOrEmpty(item))
                    {
                        // if the item is an UUID, trust the sender otherwise search the inventory
                        if (!UUID.TryParse(item, out textureUUID))
                        {
                            InventoryBase inventoryBaseItem =
                                Inventory.FindInventory<InventoryBase>(Client, Client.Inventory.Store.RootNode, item
                                    ).FirstOrDefault();
                            if (!(inventoryBaseItem is InventoryTexture))
                            {
                                throw new ScriptException(ScriptError.INVENTORY_ITEM_NOT_FOUND);
                            }
                            textureUUID = (inventoryBaseItem as InventoryTexture).AssetUUID;
                        }
                    }
                    ManualResetEvent AvatarPicksReplyEvent = new ManualResetEvent(false);
                    UUID pickUUID = UUID.Zero;
                    string name =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
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
                                .FirstOrDefault(o => string.Equals(name, o.Value, StringComparison.Ordinal));
                        if (!pick.Equals(default(KeyValuePair<UUID, string>)))
                            pickUUID = pick.Key;
                        AvatarPicksReplyEvent.Set();
                    };
                    lock (Locks.ClientInstanceAvatarsLock)
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
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(ScriptKeys.DESCRIPTION)),
                                corradeCommandParameters.Message));
                    if (Helpers.IsSecondLife(Client))
                    {
                        if (pickUUID.Equals(UUID.Zero) &&
                            pickCount >= Constants.AVATARS.PICKS.MAXIMUM_PICKS)
                        {
                            throw new ScriptException(ScriptError.MAXIMUM_AMOUNT_OF_PICKS_REACHED);
                        }
                        if (Encoding.UTF8.GetByteCount(description) >
                            Constants.AVATARS.PICKS.MAXIMUM_PICK_DESCRIPTION_SIZE)
                        {
                            throw new ScriptException(ScriptError.DESCRIPTION_WOULD_EXCEED_MAXIMUM_SIZE);
                        }
                    }
                    if (pickUUID.Equals(UUID.Zero))
                    {
                        pickUUID = UUID.Random();
                    }
                    Client.Self.PickInfoUpdate(pickUUID, false, UUID.Zero, name,
                        position, textureUUID, description);
                };
        }
    }
}