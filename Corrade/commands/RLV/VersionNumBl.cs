///////////////////////////////////////////////////////////////////////////
//  Copyright (C) Wizardry and Steamworks 2013 - License: GNU GPLv3      //
//  Please see: http://www.gnu.org/licenses/gpl.html for legal details,  //
//  rights of fair usage, the disclaimer and warranty conditions.        //
///////////////////////////////////////////////////////////////////////////

using OpenMetaverse;
using System;
using System.Globalization;
using System.Linq;
using wasOpenMetaverse;

namespace Corrade
{
    public partial class Corrade
    {
        public partial class RLVBehaviours
        {
            public static readonly Action<string, wasOpenMetaverse.RLV.RLVRule, UUID> versionnumbl =
                (message, rule, senderUUID) =>
                {
                    int channel;
                    if (!int.TryParse(rule.Param, NumberStyles.Integer, Utils.EnUsCulture, out channel) || channel < 1)
                    {
                        return;
                    }

                    Locks.ClientInstanceSelfLock.EnterWriteLock();
                    Client.Self.Chat(
                        string.Join(@",",
                            new[] {wasOpenMetaverse.RLV.RLV_CONSTANTS.LONG_VERSION}.Concat(corradeConfiguration
                                .RLVBlacklist)), channel,
                        ChatType.Normal);
                    Locks.ClientInstanceSelfLock.ExitWriteLock();
                };
        }
    }
}
