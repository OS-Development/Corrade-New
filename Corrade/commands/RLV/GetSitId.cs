///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using System;
using OpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> getsitid = (message, rule, senderUUID) =>
            {
                int channel;
                if (!int.TryParse(rule.Param, out channel) || channel < 1)
                {
                    return;
                }
                Avatar me;
                if (Client.Network.CurrentSim.ObjectsAvatars.TryGetValue(Client.Self.LocalID, out me))
                {
                    if (me.ParentID != 0)
                    {
                        Primitive sit;
                        if (Client.Network.CurrentSim.ObjectsPrimitives.TryGetValue(me.ParentID, out sit))
                        {
                            Client.Self.Chat(sit.ID.ToString(), channel, ChatType.Normal);
                            return;
                        }
                    }
                }
                UUID zero = UUID.Zero;
                Client.Self.Chat(zero.ToString(), channel, ChatType.Normal);
            };
        }
    }
}