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
using Corrade.Constants;
using CorradeConfigurationSharp;
using OpenMetaverse;
using wasOpenMetaverse;
using wasSharp;
using Inventory = wasOpenMetaverse.Inventory;
using Reflection = wasSharp.Reflection;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class CorradeCommands
        {
            public static readonly Action<Command.CorradeCommandParameters, Dictionary<string, string>> addpick =
                (corradeCommandParameters, result) =>
                {
                    if (
                        !HasCorradePermission(corradeCommandParameters.Group.UUID,
                            (int) Configuration.Permissions.Grooming))
                        throw new Command.ScriptException(Enumerations.ScriptError.NO_CORRADE_PERMISSIONS);
                    Vector3d position;
                    if (
                        !Vector3d.TryParse(
                            wasInput(KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.POSITION)),
                                corradeCommandParameters.Message)),
                            out position))
                        position = Client.Self.GlobalPosition;
                    var item =
                        wasInput(
                            KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.ITEM)),
                                corradeCommandParameters.Message));
                    var textureUUID = UUID.Zero;
                    if (!string.IsNullOrEmpty(item))
                        if (!UUID.TryParse(item, out textureUUID))
                        {
                            var inventoryBaseItem = Inventory.FindInventory<InventoryBase>(Client, item,
                                CORRADE_CONSTANTS.PATH_SEPARATOR, CORRADE_CONSTANTS.PATH_SEPARATOR_ESCAPE,
                                corradeConfiguration.ServicesTimeout);
                            if (!(inventoryBaseItem is InventoryTexture))
                                throw new Command.ScriptException(Enumerations.ScriptError.INVENTORY_ITEM_NOT_FOUND);
                            textureUUID = (inventoryBaseItem as InventoryTexture).AssetUUID;
                        }
                    var AvatarPicksReplyEvent = new ManualResetEventSlim(false);
                    var pickUUID = UUID.Zero;
                    var name =
                        wasInput(KeyValue.Get(wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.NAME)),
                            corradeCommandParameters.Message));
                    if (string.IsNullOrEmpty(name))
                        throw new Command.ScriptException(Enumerations.ScriptError.EMPTY_PICK_NAME);
                    var pickCount = 0;
                    EventHandler<AvatarPicksReplyEventArgs> AvatarPicksEventHandler = (sender, args) =>
                    {
                        if (!args.AvatarID.Equals(Client.Self.AgentID))
                            return;

                        pickCount = args.Picks.Count;
                        var pick =
                            args.Picks.AsParallel()
                                .FirstOrDefault(o => string.Equals(name, o.Value, StringComparison.Ordinal));
                        if (!pick.Equals(default(KeyValuePair<UUID, string>)))
                            pickUUID = pick.Key;
                        AvatarPicksReplyEvent.Set();
                    };
                    Locks.ClientInstanceAvatarsLock.EnterReadLock();
                    Client.Avatars.AvatarPicksReply += AvatarPicksEventHandler;
                    Client.Avatars.RequestAvatarPicks(Client.Self.AgentID);
                    if (!AvatarPicksReplyEvent.Wait((int) corradeConfiguration.ServicesTimeout))
                    {
                        Client.Avatars.AvatarPicksReply -= AvatarPicksEventHandler;
                        Locks.ClientInstanceAvatarsLock.ExitReadLock();
                        throw new Command.ScriptException(Enumerations.ScriptError.TIMEOUT_GETTING_PICKS);
                    }
                    Client.Avatars.AvatarPicksReply -= AvatarPicksEventHandler;
                    Locks.ClientInstanceAvatarsLock.ExitReadLock();
                    var description =
                        wasInput(
                            KeyValue.Get(
                                wasOutput(Reflection.GetNameFromEnumValue(Command.ScriptKeys.DESCRIPTION)),
                                corradeCommandParameters.Message));
                    if (wasOpenMetaverse.Helpers.IsSecondLife(Client))
                    {
                        if (pickUUID.Equals(UUID.Zero) &&
                            pickCount >= wasOpenMetaverse.Constants.AVATARS.PICKS.MAXIMUM_PICKS)
                            throw new Command.ScriptException(Enumerations.ScriptError.MAXIMUM_AMOUNT_OF_PICKS_REACHED);
                        if (Encoding.UTF8.GetByteCount(description) >
                            wasOpenMetaverse.Constants.AVATARS.PICKS.MAXIMUM_PICK_DESCRIPTION_SIZE)
                            throw new Command.ScriptException(
                                Enumerations.ScriptError.DESCRIPTION_WOULD_EXCEED_MAXIMUM_SIZE);
                    }
                    if (pickUUID.Equals(UUID.Zero))
                        pickUUID = UUID.Random();
                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.PickInfoUpdate(pickUUID, false, UUID.Zero, name,
                        position, textureUUID, description);
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                };
        }
    }
}