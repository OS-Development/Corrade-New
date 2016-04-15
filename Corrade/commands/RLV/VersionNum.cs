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
        public static partial class RLVBehaviours
        {
            public static Action<string, RLVRule, UUID> versionnum = (message, rule, senderUUID) =>
            {
                int channel;
                if (!int.TryParse(rule.Param, out channel) || channel < 1)
                {
                    return;
                }
                Client.Self.Chat(RLV_CONSTANTS.LONG_VERSION, channel, ChatType.Normal);
            };
        }
    }
}